# Wharf .NET migration review

## Scope

Reviewed the root Wharf migration surface only. Unrelated benchmark
directories, including Go fixtures outside the root Wharf module, were
preserved.

## Findings

- Requirements, repository memory, README, session log, and retro now identify
  .NET 10 as the canonical implementation.
- The machine-readable ledger no longer contains Go implementation constraints
  and resolves the SDK open question to `10.0.401`.
- The executable has a thin process entry point and separate command,
  configuration, logging, versioning, and exit-code namespaces.
- The release version is controlled by the `WharfVersion` MSBuild property and
  produces semver-only version output.
- Unknown command-line input is rejected by System.CommandLine, and all
  operational placeholders fail with a non-success exit code.
- Build, tests, format/analyzer verification, JSON output, help, completion
  validation, and compiled CLI checks passed.

## Follow-up

The full Session 1 contract still has deliberate follow-up work for richer
redaction hooks, repeatable verbosity, golden help tests, and durable logging.
Those belong before or during the next implementation pass and are not
represented as successful operational behavior in this replacement foundation.
