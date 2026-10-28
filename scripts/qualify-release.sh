#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${1:-0.20.0}"
OWNER="${WHARF_RELEASE_OWNER:-Wharf maintainers}"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

cd "$ROOT"
echo "Wharf release qualification"
echo "version=$VERSION"
echo "owner=$OWNER"
echo
dotnet format wharf.slnx --verify-no-changes
dotnet build wharf.slnx --configuration Release --no-restore
dotnet test tests/wharf.Tests/wharf.Tests.csproj --configuration Release --no-restore

if git grep -nE 'BEGIN (RSA|OPENSSH|EC|DSA) PRIVATE KEY|ghp_[A-Za-z0-9]{20,}|sk-[A-Za-z0-9]{20,}' -- \
  ':!docs/release-qualification.md'; then
  echo "secret scan failed" >&2
  exit 1
fi

PATH="${HOME}/.dotnet:${PATH}" "$ROOT/scripts/package-wharf.sh" "$VERSION" "$WORK/package" >/dev/null
dpkg-deb --extract "$WORK/package/wharf_${VERSION}_amd64.deb" "$WORK/extracted"
PACKAGED_VERSION="$("$WORK/extracted/usr/bin/wharf" --version)"
if [[ "$PACKAGED_VERSION" != "$VERSION" ]]; then
  echo "packaged executable version mismatch: expected $VERSION, got $PACKAGED_VERSION" >&2
  exit 1
fi
echo "packaged-executable=$PACKAGED_VERSION"
(
  cd "$WORK/package"
  sha256sum -c SHA256SUMS
  test -s SBOM.json
  if [[ -n "${SIGNING_KEY:-}" ]]; then
    test -s "wharf_${VERSION}_amd64.deb.asc"
  else
    test -f SIGNING.txt
    echo "signing=not configured"
  fi
)
echo "qualification=automated gates passed; see docs/release-qualification.md for evidence-backed gates"
