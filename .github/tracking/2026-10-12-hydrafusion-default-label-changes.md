# HydraFusion Default Label Changes

## Changes

- Added shared effective-effort normalization that maps supported HydraFusion
  effort inputs to `default`.
- Applied the normalization to resolved run configuration and job directory
  naming.
- Preserved invalid effort values so normal configuration validation still
  rejects them explicitly.
- Updated configuration and naming tests for HydraFusion default behavior.
- Changed both README result labels to HydraFusion plus version only.

## Validation

- Focused configuration, run-service, and Copilot adapter tests: 33 passed,
  0 failed.
- Build performed by `dotnet test`: succeeded.
- `git diff --check`: passed.
