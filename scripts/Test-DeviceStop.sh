#!/usr/bin/env bash
# Offline regression test for the real stop-fullnet.sh ownership function.
# Never touches /userdata, KNULLI networking, or other applications.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
sourcefile="$root/native/device/stop-fullnet.sh"
extract="$(awk '/^stop_owned_pid\(\) \{/ { capture=1 } capture { print } capture && /^}/ { exit }' "$sourcefile")"
[[ "$extract" == *"preserving"* ]] || { echo "Missing ownership-preserving stop logic"; exit 1; }
# Source only the function definition; do not run the device-side script.
eval "$extract"

work="$(mktemp -d)"
pids=""
cleanup() {
  for pid in $pids; do
    kill -KILL "$pid" 2>/dev/null || true
    wait "$pid" 2>/dev/null || true
  done
  rm -rf "$work"
}
trap cleanup EXIT

spawn() {
  local mode="$1" marker="$2"
  if [[ "$mode" == "resistant" ]]; then
    python3 -c 'import signal,time;signal.signal(signal.SIGTERM,signal.SIG_IGN);time.sleep(60)' "$marker" &
  else
    python3 -c 'import time;time.sleep(60)' "$marker" &
  fi
  spawned=$!
  pids="$pids $spawned"
  for _ in $(seq 1 30); do
    if [[ -r "/proc/$spawned/cmdline" ]] &&
      tr '\000' ' ' < "/proc/$spawned/cmdline" | grep -Fq "$marker"; then
      sleep 0.3
      return
    fi
    sleep 0.1
  done
  echo "Test helper never started: $marker" >&2
  exit 1
}

# Owned helper exits; the PID record disappears only after /proc confirms it.
spawn normal "rgusb-owned-ok"
echo "$spawned" > "$work/owned.pid"
FAILED=0
stop_owned_pid "$work/owned.pid" "rgusb-owned-ok"
[[ "$FAILED" == 0 && ! -e "$work/owned.pid" ]] ||
  { echo "Owned helper PID was not safely cleaned up"; exit 1; }

# A command-line mismatch must NOT terminate an unrelated process.
spawn normal "rgusb-unrelated"
echo "$spawned" > "$work/unknown.pid"
FAILED=0
stop_owned_pid "$work/unknown.pid" "rgusb-expected-owned"
[[ "$FAILED" == 1 && -f "$work/unknown.pid" ]] ||
  { echo "Unowned live PID incorrectly cleared"; exit 1; }
kill -0 "$spawned"

# A helper refusing SIGTERM must preserve ownership evidence and fail STOP.
spawn resistant "rgusb-stubborn-owned"
echo "$spawned" > "$work/stubborn.pid"
FAILED=0
stop_owned_pid "$work/stubborn.pid" "rgusb-stubborn-owned"
[[ "$FAILED" == 1 && -f "$work/stubborn.pid" ]] ||
  { echo "Stubborn owned PID lost or STOP falsely succeeded"; exit 1; }
kill -0 "$spawned"

# A truly dead PID is stale and safe to forget.
echo 99999999 > "$work/stale.pid"
FAILED=0
stop_owned_pid "$work/stale.pid" "rgusb-no-such-helper"
[[ "$FAILED" == 0 && ! -e "$work/stale.pid" ]] ||
  { echo "Stale PID record remained"; exit 1; }
echo "Device helper STOP tests passed: exited, unrelated, stubborn, stale."
