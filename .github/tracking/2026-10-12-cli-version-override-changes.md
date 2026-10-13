# CLI Version Override Changes

## Changes

- Added `CliVersion` to run configuration overrides.
  - Appended it as an optional record parameter to preserve existing positional
    caller compatibility.
- Added `--cli-version` to `wharf run`.
- Added `WHARF_CLI_VERSION` resolution with
  flag-over-environment-over-manifest-over-default precedence.
- Wired normal run, dry-run, and smoke environment resolution.
- Added focused precedence tests and CLI documentation.

## Validation

- `dotnet test tests/wharf.Tests/wharf.Tests.csproj --filter FullyQualifiedName~RunConfigurationTests`
  - Passed: 20
  - Failed: 0
- `wharf run --help`
  - Shows `--cli-version <cli-version>`.
- Dry-run with `WHARF_CLI_VERSION=1.0.94`
  - Rendered `cliVersion` as `1.0.94`.
- Dry-run with `WHARF_CLI_VERSION=1.0.94 --cli-version 1.0.95`
  - Rendered `cliVersion` as `1.0.95`.
