#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-}"
OUTPUT="${2:-dist}"
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "usage: $0 VERSION [OUTPUT_DIRECTORY]" >&2
  exit 2
fi

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT="$(mkdir -p "$OUTPUT" && cd "$OUTPUT" && pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
PACKAGE_ROOT="$WORK/package"
mkdir -p "$PACKAGE_ROOT/DEBIAN" "$PACKAGE_ROOT/usr/bin" "$PACKAGE_ROOT/usr/lib/wharf" \
  "$PACKAGE_ROOT/usr/share/doc/wharf" "$PACKAGE_ROOT/usr/share/wharf"

dotnet publish "$ROOT/src/wharf/wharf.csproj" --configuration Release --no-restore \
  -p:WharfVersion="$VERSION" --output "$WORK/publish" >/dev/null
cp -a "$WORK/publish/." "$PACKAGE_ROOT/usr/lib/wharf/"
chmod 0755 "$PACKAGE_ROOT/usr/lib/wharf/wharf"
ln -s ../lib/wharf/wharf "$PACKAGE_ROOT/usr/bin/wharf"
cp "$ROOT/scripts/wharf-bootstrap.sh" "$PACKAGE_ROOT/usr/share/wharf/bootstrap.sh"
cp "$ROOT/README.md" "$PACKAGE_ROOT/usr/share/doc/wharf/README.md"
cat > "$PACKAGE_ROOT/DEBIAN/control" <<EOF
Package: wharf
Version: $VERSION
Section: utils
Priority: optional
Architecture: amd64
Depends: dotnet-runtime-10.0
Maintainer: Wharf maintainers
Description: Trustworthy software evaluation CLI
EOF
cat > "$PACKAGE_ROOT/DEBIAN/postinst" <<'EOF'
#!/bin/sh
set -eu
chmod 0755 /usr/lib/wharf/wharf
EOF
chmod 0755 "$PACKAGE_ROOT/DEBIAN/postinst"

PACKAGE="$OUTPUT/wharf_${VERSION}_amd64.deb"
dpkg-deb --build --root-owner-group "$PACKAGE_ROOT" "$PACKAGE" >/dev/null
(
  cd "$OUTPUT"
  sha256sum "$(basename "$PACKAGE")" > SHA256SUMS
  python3 - "$PACKAGE" "$VERSION" > SBOM.json <<'PY'
import hashlib, json, pathlib, sys
package = pathlib.Path(sys.argv[1])
print(json.dumps({
    "bomFormat": "CycloneDX",
    "specVersion": "1.5",
    "version": 1,
    "metadata": {"component": {"name": "wharf", "version": sys.argv[2]}},
    "components": [{
        "type": "file",
        "name": path.name,
        "version": sys.argv[2],
        "hashes": [{"alg": "SHA-256", "content": hashlib.sha256(path.read_bytes()).hexdigest()}]
    } for path in sorted(package.parent.glob("*")) if path.is_file() and path.name != "SBOM.json"]
}, sort_keys=True, indent=2))
PY
  if [[ -n "${SIGNING_KEY:-}" ]]; then
    gpg --batch --yes --local-user "$SIGNING_KEY" --detach-sign --armor "$PACKAGE"
    gpg --batch --verify "$PACKAGE.asc" "$PACKAGE"
  else
    echo "unsigned: set SIGNING_KEY to create and verify a detached signature" > SIGNING.txt
  fi
)
echo "$PACKAGE"
