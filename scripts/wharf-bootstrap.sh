#!/usr/bin/env bash
set -euo pipefail

PACKAGE="${1:-}"
if [[ -z "$PACKAGE" || ! -f "$PACKAGE" ]]; then
  echo "usage: $0 PACKAGE.deb" >&2
  exit 2
fi
if [[ "${EUID:-$(id -u)}" -ne 0 ]]; then
  exec sudo "$0" "$@"
fi

STATE=/var/lib/wharf
mkdir -p "$STATE"
if dpkg-query -W -f='${Status}' wharf 2>/dev/null | grep -q 'install ok installed'; then
  dpkg-query -W -f='${Version}' wharf > "$STATE/previous-version"
  dpkg-repack wharf > "$STATE/previous.deb"
fi
dpkg --install "$PACKAGE"
dpkg-query -W -f='${Version}\n' wharf > "$STATE/current-version"
echo "installed wharf $(cat "$STATE/current-version")"
