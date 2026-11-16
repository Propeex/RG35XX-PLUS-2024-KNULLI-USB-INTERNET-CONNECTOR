#!/bin/sh
# RG35XX 2024 / KNULLI USB Full Internet v1.5 compatibility add-on.
# Keeps the v1.4 native TUN path intact. Idempotent and conservative.
set -eu
BASE=/userdata/system/rgusb-fullnet
TUN=rgusb0
ALIAS_IP=192.0.2.123/32
PM=/userdata/roms/ports/PortMaster.sh
PM_MARKER=RGUSB_V15_PM_PROXY_FIX

has_alias() {
  ip -4 addr show dev "$TUN" 2>/dev/null | grep -Fq "inet $ALIAS_IP "
}
apply() {
  [ -f "$BASE/active" ] || { echo 'ERROR: USB native TUN is not active.' >&2; exit 10; }
  ip link show "$TUN" >/dev/null 2>&1 || { echo 'ERROR: rgusb0 is missing.' >&2; exit 11; }

  if has_alias; then
    echo 'KNULLI IP alias: already present.'
  else
    echo 'Applying KNULLI IP alias rgusb0:eth0 (192.0.2.123/32)...'
    ip addr add "$ALIAS_IP" dev "$TUN" label "$TUN:eth0" || {
      echo 'ERROR: could not add rgusb0:eth0 alias.' >&2; exit 12;
    }
    : > "$BASE/alias.created"
  fi
  has_alias || { echo 'ERROR: IP alias could not be verified.' >&2; exit 13; }

  if [ ! -f "$PM" ]; then
    echo 'PortMaster launcher: not installed (skipping).'
    return 0
  fi
  if grep -Fq "$PM_MARKER" "$PM"; then
    echo 'PortMaster SOCKS fix: already present (v1.5).'
    return 0
  fi
  if grep -Fq 'PM_NO_SOCKS_FIX' "$PM"; then
    echo 'PortMaster SOCKS fix: already present (verified older manual fix).'
    return 0
  fi
  if [ -L "$PM" ]; then
    echo 'ERROR: PortMaster launcher is a symlink; refusing to edit it.' >&2
    exit 14
  fi
  if ! head -n 1 "$PM" | grep -q '^#!'; then
    echo 'ERROR: PortMaster launcher lacks a shebang; refusing to edit.' >&2
    exit 15
  fi
  if [ ! -e "$PM.bak_rgusb_v15" ]; then
    cp -p "$PM" "$PM.bak_rgusb_v15" || {
      echo 'ERROR: cannot back up PortMaster launcher.' >&2; exit 16;
    }
  fi
  sed -i '2i unset ALL_PROXY all_proxy HTTP_PROXY http_proxy HTTPS_PROXY https_proxy FTP_PROXY ftp_proxy # RGUSB_V15_PM_PROXY_FIX' "$PM" || {
    echo 'ERROR: cannot patch PortMaster launcher.' >&2; exit 17;
  }
  if ! grep -Fq "$PM_MARKER" "$PM"; then
    echo 'ERROR: PortMaster launcher patch was not verified.' >&2; exit 18
  fi
  chmod +x "$PM" || { echo 'ERROR: PortMaster launcher is not executable.' >&2; exit 19; }
  echo 'PortMaster SOCKS fix: applied (original backed up).'
}

restore_pm() {
  [ -f "$PM" ] || { echo 'PortMaster launcher: not present; nothing to restore.'; return 0; }
  if grep -Fq "$PM_MARKER" "$PM"; then
    sed -i '/^unset ALL_PROXY all_proxy HTTP_PROXY http_proxy HTTPS_PROXY https_proxy FTP_PROXY ftp_proxy # RGUSB_V15_PM_PROXY_FIX$/d' "$PM"
    if grep -Fq "$PM_MARKER" "$PM"; then
      echo 'ERROR: v1.5 patch marker remains; manual inspection needed.' >&2; exit 20
    fi
    echo 'Removed the v1.5 PortMaster proxy override; backup remains.'
  elif grep -Fq 'PM_NO_SOCKS_FIX' "$PM"; then
    echo 'Existing manual PortMaster fix was left untouched (see PortMaster.sh.bak_usb).'
  else
    echo 'No v1.5 PortMaster patch found; nothing to restore.'
  fi
}
case "${1:-apply}" in
  apply) apply ;;
  restore-portmaster) restore_pm ;;
  *) echo 'Usage: compat-fullnet.sh apply|restore-portmaster' >&2; exit 2 ;;
esac
