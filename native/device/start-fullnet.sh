#!/bin/sh
# RG35XX 2024 / KNULLI, nonpersistent USB TUN connection.
# Requires active SSH dynamic SOCKS at 127.0.0.1:1080.
# v1.4: helper processes are launched in independent sessions via daemon-launch,
# so they survive the end of the ADB shell that started the tunnel.
set -eu
BASE=/userdata/system/rgusb-fullnet
TUN=rgusb0
mkdir -p "$BASE"
if [ -f "$BASE/active" ]; then
  if [ -r "$BASE/dns.pid" ] && [ -r "$BASE/tun.pid" ] \
      && kill -0 "$(cat "$BASE/dns.pid")" 2>/dev/null \
      && kill -0 "$(cat "$BASE/tun.pid")" 2>/dev/null; then
    echo 'ALREADY ACTIVE: rgusb0 full-network tunnel.'
    exit 0
  fi
  echo 'NOTICE: stale tunnel state found; cleaning up before retry.'
  sh "$BASE/stop-fullnet.sh"
fi
# Never steal an existing internet route from Wi-Fi / Ethernet.
if ip -4 route show default 2>/dev/null | grep -q '^default'; then
  echo 'ERROR: A default IPv4 route is already present; refusing to override the existing connection.' >&2
  exit 2
fi
[ -x "$BASE/tun2socks" ] || { echo 'ERROR: tun2socks binary missing.' >&2; exit 3; }
[ -x "$BASE/dns-bridge" ] || { echo 'ERROR: dns-bridge binary missing.' >&2; exit 3; }
[ -x "$BASE/tunctl" ] || { echo 'ERROR: tunctl binary missing.' >&2; exit 3; }
[ -x "$BASE/daemon-launch" ] || { echo 'ERROR: daemon-launch binary missing.' >&2; exit 3; }
if ! curl -fsSI --max-time 10 --socks5-hostname 127.0.0.1:1080 https://example.com >/dev/null 2>&1; then
  echo 'ERROR: existing USB SOCKS proxy at 127.0.0.1:1080 is not reachable.' >&2
  exit 5
fi
cleanup_on_failure() {
  status=$?
  trap - 0
  if [ "$status" -ne 0 ]; then
    echo "SETUP FAILED (exit $status); rolling back rgusb0 and resolver..." >&2
    sh "$BASE/stop-fullnet.sh" || true
  fi
}
trap cleanup_on_failure 0

# CONFIG_TUN=y was verified from the user's running kernel. Create the standard
# transient device node if this minimal Linux image has not populated /dev/net.
if [ ! -c /dev/net/tun ]; then
  mkdir -p /dev/net
  mknod /dev/net/tun c 10 200 || { echo 'ERROR: cannot create /dev/net/tun.' >&2; exit 4; }
  chmod 600 /dev/net/tun
  : > "$BASE/tun.node.created"
fi

echo 'Creating temporary TUN interface...'
"$BASE/tunctl" create "$TUN"
: > "$BASE/tun.created"
ip address add 198.18.0.1/15 dev "$TUN"
ip link set dev "$TUN" up

# Listen on 127.0.0.2 to avoid colliding with ConnMan's 127.0.0.1:53 DNS proxy.
echo 'Starting local DNS-over-HTTPS helper...'
"$BASE/daemon-launch" "$BASE/dns.pid" "$BASE/dns.log" \
  "$BASE/dns-bridge" --listen 127.0.0.2:53 --socks 127.0.0.1:1080 \
  --doh https://cloudflare-dns.com/dns-query
sleep 2
kill -0 "$(cat "$BASE/dns.pid")" 2>/dev/null || { echo 'ERROR: DNS helper exited; check dns.log.' >&2; exit 7; }

echo 'Starting tun2socks...'
"$BASE/daemon-launch" "$BASE/tun.pid" "$BASE/tun2socks.log" \
  "$BASE/tun2socks" --device tun://"$TUN" \
  --proxy socks5://127.0.0.1:1080 --loglevel warning
sleep 2
kill -0 "$(cat "$BASE/tun.pid")" 2>/dev/null || { echo 'ERROR: tun2socks exited; check tun2socks.log.' >&2; exit 8; }

# Direct edit of temporary resolver target, not a bind mount (ADB may use
# a different mount namespace in a subsequent shell).
RESOLV_TARGET="$(readlink -f /etc/resolv.conf 2>/dev/null || echo /etc/resolv.conf)"
[ -f "$RESOLV_TARGET" ] || { echo "ERROR: resolver target does not exist: $RESOLV_TARGET" >&2; exit 9; }
cp -p "$RESOLV_TARGET" "$BASE/resolv.backup" || { echo 'ERROR: cannot back up resolver' >&2; exit 9; }
echo "$RESOLV_TARGET" > "$BASE/resolv.target"
printf 'nameserver 127.0.0.2\noptions timeout:2 attempts:2\n' > "$BASE/resolv.conf"
printf 'nameserver 127.0.0.2\noptions timeout:2 attempts:2\n' > "$RESOLV_TARGET" || { echo 'ERROR: cannot write resolver' >&2; exit 9; }
: > "$BASE/resolv.written"

# No other default route was present. Adding this is safe to undo.
ip -4 route add default dev "$TUN"
: > "$BASE/route.created"

echo 'Testing direct HTTPS without ALL_PROXY...'
if ! curl --noproxy '*' -fsSI --max-time 22 https://example.com > "$BASE/native-test.txt" 2> "$BASE/native-test.err"; then
  echo 'ERROR: direct HTTPS failed. See native-test.err, dns.log, tun2socks.log.' >&2
  cat "$BASE/native-test.err" >&2 || true
  exit 10
fi
head -n 1 "$BASE/native-test.txt"
: > "$BASE/active"
# If USB tunnel breaks for a while, automatically remove the transient default route.
"$BASE/daemon-launch" "$BASE/watch.pid" "$BASE/watch.log" \
  /bin/sh "$BASE/watch-fullnet.sh"
trap - 0
echo 'FULL NETWORK TEST SUCCESS: native DNS + HTTPS through USB TUN.'
echo 'Note: ConnMan Wi-Fi icon / UDP games are not guaranteed.'
