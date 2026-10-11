The Wharf 0.17.0 release has no packaging workflow. Implement the 0.17.0 to
0.18.0 packaging and supply-chain boundary.

Add the historical scripts and behavior for:

- building a versioned amd64 Debian package from the published Wharf binary;
- installing through a privileged bootstrap script with previous-version
  capture;
- rolling back to the retained previous package;
- writing SHA256 checksums and a deterministic CycloneDX SBOM;
- optionally creating and verifying a detached signature using `SIGNING_KEY`;
- rejecting malformed or missing inputs with explicit nonzero exit codes.

Preserve existing source behavior, do not modify existing test files, and keep
privilege escalation explicit. The implementation should match the target
release's scripts and observable contracts. Do not commit changes; the
 evaluator captures the working-tree patch after implementation.

Provide the historical scripts at exactly these paths:
`scripts/package-wharf.sh`, `scripts/wharf-bootstrap.sh`, and
`scripts/wharf-rollback.sh`. The package script must use `dpkg-deb --build`,
produce SHA-256 checksums, and include an explicit SBOM step. The bootstrap
and rollback scripts must invoke privileged operations with `exec sudo`; the
rollback script must support restoring `previous.deb`.
