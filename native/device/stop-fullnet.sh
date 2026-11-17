#!/bin/sh
# RG35XX KNULLI / USB Full Internet v1.6.
# Reverts only networking that start-fullnet.sh created. Idempotent.
BASE=/userdata/system/rgusb-fullnet
TUN=rgusb0
FAILED=0

# Revert only the temporary KNULLI address alias.
if [ -f "$BASE/alias.created" ]; then
  ip addr del 192.0.2.123/32 dev "$TUN" >/dev/null 2>&1 || true
  rm -f "$BASE/alias.created"
fi

# Only remove OUR default route, and only if start script marked it.
if [ -f "$BASE/route.created" ]; then
  ip -4 route del default dev "$TUN" >/dev/null 2>&1 || true
  if ip -4 route show default 2>/dev/null | grep -Fq "dev $TUN"; then
    echo 'WARNING: own default route was not removed.' >&2
    FAILED=1
  else
    rm -f "$BASE/route.created"
  fi
fi

# Restore resolver only if it still contains our exact nameserver contents.
if [ -f "$BASE/resolv.written" ] && [ -r "$BASE/resolv.target" ]; then
  TARGET="$(cat "$BASE/resolv.target")"
  if [ -f "$TARGET" ] && [ -r "$BASE/resolv.backup" ]; then
    if cmp -s "$BASE/resolv.conf" "$TARGET"; then
      if cp -p "$BASE/resolv.backup" "$TARGET"; then
        rm -f "$BASE/resolv.written" "$BASE/resolv.backup"
      else
        echo "WARNING: could not restore original resolver: $TARGET" >&2
        FAILED=1
      fi
    else
      echo 'NOTICE: resolver changed since setup; preserving current contents.'
      rm -f "$BASE/resolv.written" "$BASE/resolv.backup"
    fi
  else
    echo "WARNING: resolver backup/target unavailable: $TARGET" >&2
    FAILED=1
  fi
fi

# Legacy bind resolver from early versions.
if [ -f "$BASE/resolv.mounted" ] && [ -r "$BASE/resolv.target" ]; then
  TARGET="$(cat "$BASE/resolv.target")"
  if umount "$TARGET" >/dev/null 2>&1; then
    rm -f "$BASE/resolv.mounted"
  else
    echo 'WARNING: legacy resolver bind mount remains.' >&2
    FAILED=1
  fi
fi

# Stop ONLY helper processes whose /proc cmdline contains our exact helper path.
stop_owned_pid() {
  PIDFILE="$1"
  EXPECTED="$2"
  [ -r "$PIDFILE" ] || return 0
  PID="$(cat "$PIDFILE")"
  case "$PID" in
    ''|*[!0-9]*) echo "WARNING: invalid PID file: $PIDFILE" >&2; FAILED=1; return 0;;
  esac
  if [ -r "/proc/$PID/cmdline" ]; then
    if tr '\000' ' ' < "/proc/$PID/cmdline" | grep -Fq "$EXPECTED"; then
      kill "$PID" 2>/dev/null || true
      I=0
      while [ "$I" -lt 4 ]; do
        if [ ! -r "/proc/$PID/cmdline" ]; then
          rm -f "$PIDFILE"
          return 0
        fi
        if [ -r "/proc/$PID/stat" ]; then
          ST="$(awk '{print $3}' "/proc/$PID/stat" 2>/dev/null)"
          if [ "$ST" = Z ]; then
            rm -f "$PIDFILE"
            return 0
          fi
        fi
        sleep 1
        I=$((I+1))
      done
      # Never forget a still-running helper. A future manual STOP must be
      # able to retry cleanup using the original ownership evidence.
      echo "WARNING: owned PID $PID is still alive after SIGTERM; preserving $PIDFILE." >&2
      FAILED=1
      return 0
    else
      echo "WARNING: PID $PID belongs to another process; will not kill." >&2
      FAILED=1
      return 0
    fi
  fi
  # Process already exited before STOP began; this marker is safely stale.
  rm -f "$PIDFILE"
}
stop_owned_pid "$BASE/tun.pid" "$BASE/tun2socks"
stop_owned_pid "$BASE/dns.pid" "$BASE/dns-bridge"

# A TUN fd can keep the interface alive briefly; delete after stopping helper.
if [ -f "$BASE/tun.created" ]; then
  if ip link show "$TUN" >/dev/null 2>&1; then
    ip link set dev "$TUN" down >/dev/null 2>&1 || true
    "$BASE/tunctl" delete "$TUN" >/dev/null 2>&1 || \
      ip link del dev "$TUN" >/dev/null 2>&1 || true
    sleep 1
  fi
  if ip link show "$TUN" >/dev/null 2>&1; then
    echo 'WARNING: rgusb0 still exists; keeping creation marker for recovery.' >&2
    FAILED=1
  else
    rm -f "$BASE/tun.created"
  fi
fi

# Avoid removing /dev/net/tun while a leftover TUN still exists.
if [ -f "$BASE/tun.node.created" ] && ! ip link show "$TUN" >/dev/null 2>&1; then
  rm -f /dev/net/tun 2>/dev/null || true
  rm -f "$BASE/tun.node.created"
fi
[ -f "$BASE/resolv.mounted" ] || [ -f "$BASE/resolv.written" ] || rm -f "$BASE/resolv.target"

# Stop the watcher LAST; it may itself have invoked this script.
stop_owned_pid "$BASE/watch.pid" "$BASE/watch-fullnet.sh"

if [ "$FAILED" -eq 0 ] && ! ip link show "$TUN" >/dev/null 2>&1; then
  rm -f "$BASE/active"
  echo 'STOP COMPLETE: rgusb0, DNS helper and owned default route removed.'
  exit 0
fi
echo 'STOP INCOMPLETE: an owned network artifact remains; see warnings.' >&2
exit 1
