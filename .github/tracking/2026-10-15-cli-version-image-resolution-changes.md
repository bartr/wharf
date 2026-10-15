# CLI Version Image Resolution Changes

## Changes

- Exact requested CLI versions are now normalized and used directly as the
  requested/effective version without probing the host-installed executable.
- `latest` now resolves `@github/copilot` metadata from the npm registry and
  fails explicitly on transport, HTTP, malformed JSON, or missing-version
  errors.
- Run setup uses the registry resolver rather than `copilot --version`.
- Removed the unused host-process version probe.
- Added tests proving exact versions do not call the resolver, `latest` does,
  and registry metadata and HTTP failures are handled explicitly.
- Updated operator documentation with exact-version and `latest` semantics.

## Validation

- Focused adapter and image preparer tests:
  - Passed: 20
  - Failed: 0
- Full Release suite:
  - Passed: 229
  - Failed: 0
  - The Docker-backed smoke test built
    `wharf-copilot-cli:1.0.98-linux-amd64-v1`, composed its agent cache, and
    passed agent and verifier execution.
- `dotnet format wharf.slnx --verify-no-changes`
  - Passed with no formatting changes required.
