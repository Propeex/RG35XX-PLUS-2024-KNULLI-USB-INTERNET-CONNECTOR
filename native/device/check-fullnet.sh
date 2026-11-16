#!/bin/sh
BASE=/userdata/system/rgusb-fullnet
echo '=== KNULLI USB full-network status ==='
if [ -f "$BASE/active" ]; then echo 'ACTIVE FLAG: present'; else echo 'ACTIVE FLAG: absent'; fi
if ip link show rgusb0 >/dev/null 2>&1; then echo 'TUN: rgusb0 present'; else echo 'TUN: unavailable'; fi
if ip -4 addr show dev rgusb0 2>/dev/null | grep -Fq 'inet 192.0.2.123/32 '; then
  echo 'KNULLI IP detection alias: present (192.0.2.123/32)'
else
  echo 'KNULLI IP detection alias: missing'
fi
if [ -f /userdata/roms/ports/PortMaster.sh ]; then
  if grep -Fq 'RGUSB_V15_PM_PROXY_FIX' /userdata/roms/ports/PortMaster.sh || \
     grep -Fq 'PM_NO_SOCKS_FIX' /userdata/roms/ports/PortMaster.sh; then
    echo 'PortMaster SOCKS override: present'
  else
    echo 'PortMaster SOCKS override: absent'
  fi
else
  echo 'PortMaster launcher: not installed'
fi
for n in dns tun watch; do
  if [ -r "$BASE/$n.pid" ]; then
    p="$(cat "$BASE/$n.pid")"
    if [ -d "/proc/$p" ]; then echo "$n helper: PID $p present"; else echo "$n helper: PID $p gone"; fi
  else
    echo "$n helper: PID unavailable"
  fi
done
echo 'IPv4 routes:'
ip -4 route show 2>&1
echo 'Resolver:'
cat /etc/resolv.conf 2>&1
if [ -f "$BASE/active" ]; then
  echo 'Native HTTP test (no proxy options):'
  curl --noproxy '*' --max-time 15 -sSI https://example.com 2>&1 | head -n 5
fi
