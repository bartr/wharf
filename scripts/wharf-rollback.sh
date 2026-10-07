#!/usr/bin/env bash
set -euo pipefail
STATE=/var/lib/wharf
if [[ "${EUID:-$(id -u)}" -ne 0 ]]; then
  exec sudo "$0" "$@"
fi
if [[ ! -s "$STATE/previous.deb" ]]; then
  echo "no previous Wharf package is available for rollback" >&2
  exit 2
fi
dpkg --install "$STATE/previous.deb"
dpkg-query -W -f='${Version}\n' wharf > "$STATE/current-version"
echo "rolled back wharf $(cat "$STATE/current-version")"
