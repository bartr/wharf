# CLI Version Image Resolution Plan

1. [x] Correct adapter version resolution.
   - Target: `src/wharf/Copilot/CopilotContracts.cs`
   - For an exact requested version, normalize it and use it as the effective
     version without invoking the probe.
   - For `latest`, invoke the probe and normalize the returned published
     version.
   - Exit: exact versions are independent of probe output and `latest` still
     produces one exact requested/effective version.

2. [x] Add registry-backed latest resolution.
   - Targets: `src/wharf/Copilot/CopilotContracts.cs`,
     `src/wharf/Run/RunService.cs`
   - Resolve the npm registry metadata for `@github/copilot` and return its
     `latest` distribution tag through `ICopilotCliProbe`.
   - Surface network, HTTP, missing-tag, and malformed-version failures as
     explicit adapter errors.
   - Exit: normal runs no longer invoke the host `copilot --version`.

3. [x] Cover exact and latest behavior.
   - Target: `tests/wharf.Tests/CopilotAdapterTests.cs`
   - Prove exact selection does not invoke the probe.
   - Prove `latest` uses probe output.
   - Add focused parsing/failure tests for registry metadata without network
     access.
   - Exit: focused tests pass and encode the corrected contract.

4. [x] Update operator documentation.
   - Target: `docs/wharf.md`
   - State that exact versions drive image preparation independently of the
     host CLI and that `latest` resolves from the npm registry.
   - Exit: documented behavior matches implementation.

5. [x] Validate and review.
   - Run focused Copilot adapter and image preparer tests, then the repository
     build.
   - Record implementation evidence in the changes artifact and final
     verification in the review artifact.
   - Exit: tests and build pass, or failures are documented precisely.

## Parking Lot

- Registry caching or a configurable registry endpoint can be considered
  separately if repeated `latest` resolution becomes an operational concern.
