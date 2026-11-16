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
ROOT_HOME=/userdata/system
if [ ! -d "$ROOT_HOME" ]; then
  ROOT_HOME="$(awk -F: '$1 == "root" {print $6; exit}' /etc/passwd 2>/dev/null)"
  [ -n "$ROOT_HOME" ] || ROOT_HOME=/root
fi
[ -d "$ROOT_HOME" ] || { echo "ERROR: root home not found: $ROOT_HOME" >&2; exit 3; }
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
