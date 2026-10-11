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

The public distribution entrypoints remain:
`scripts/package-wharf.sh`, `scripts/wharf-bootstrap.sh`, and
`scripts/wharf-rollback.sh`.

The package script must build with standard Debian tooling, emit a verifiable
SHA-256 checksum, and create deterministic CycloneDX JSON describing the
package. When `SIGNING_KEY` is set, create and verify a detached signature.
Bootstrap and rollback must re-exec through `sudo` when unprivileged rather
than mixing privileged and unprivileged steps. Preserve the installed package
before replacement and require a nonempty retained package before rollback.
