#!/bin/sh
# Register only the requested SSH ed25519 public key for KNULLI root.
set -eu
PUB="${1:-}"
case "$PUB" in
  'ssh-ed25519 '*) ;;
  *) echo 'ERROR: only OpenSSH ed25519 public keys are supported' >&2; exit 2;;
esac
# Limit input: no single quotes, shell substitutions, dollar signs or slashes.
case "$PUB" in
  *"'"*|*'`'*|*'$'*|*'\'*|*'\r'*|*'\n'*) echo 'ERROR: unsafe public key characters' >&2; exit 2;;
esac
# SSH (including Dropbear) resolves ~/.ssh relative to the root account's
# passwd home. KNULLI normally uses /userdata/system, but do not register
# a key in the wrong directory when a firmware image differs.
ROOT_HOME="$(awk -F: '$1 == "root" {print $6; exit}' /etc/passwd 2>/dev/null)"
case "$ROOT_HOME" in
  /*) ;;
  *) ROOT_HOME=/userdata/system ;;
esac
[ -d "$ROOT_HOME" ] || { echo "ERROR: SSH root account home unavailable: $ROOT_HOME" >&2; exit 3; }
DIR="$ROOT_HOME/.ssh"
AUTH="$DIR/authorized_keys"
[ ! -L "$DIR" ] || { echo 'ERROR: .ssh is symlink, refusing to modify' >&2; exit 4; }
[ ! -L "$AUTH" ] || { echo 'ERROR: authorized_keys is symlink, refusing to modify' >&2; exit 4; }
mkdir -p "$DIR"
chmod 700 "$DIR"
if [ ! -f "$AUTH" ]; then
  (umask 077; : > "$AUTH")
fi
chmod 600 "$AUTH"
if grep -Fqx "$PUB" "$AUTH"; then
  echo 'SSH public key: already installed.'
else
  printf '%s\n' "$PUB" >> "$AUTH"
  echo 'SSH public key: installed without removing existing keys.'
fi

# Do not rely only on adb.exe's return code: some adbd variants hide the
# remote shell exit code. Windows requires this postcondition marker.
if ! grep -Fqx "$PUB" "$AUTH"; then
  echo 'ERROR: SSH authorized_keys does not contain the requested key.' >&2
  exit 6
fi
echo 'RGUSB_SSH_KEY_READY'
