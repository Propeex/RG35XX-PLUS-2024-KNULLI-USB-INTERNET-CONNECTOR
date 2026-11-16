#!/bin/sh
BASE=/userdata/system/rgusb-fullnet
failures=0
while [ -f "$BASE/active" ]; do
  sleep 20
  [ -f "$BASE/active" ] || exit 0
  if curl -fsSI --max-time 8 --socks5-hostname 127.0.0.1:1080 https://example.com >/dev/null 2>&1; then
    failures=0
  else
    failures=$((failures+1))
    echo "USB SOCKS health failure #$failures"
    if [ "$failures" -ge 3 ]; then
      echo 'USB has been unavailable for ~60s. Reverting temporary routing.'
      sh "$BASE/stop-fullnet.sh" || true
      exit 0
    fi
  fi
done
