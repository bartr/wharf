# Structured stage events changes

## Implementation

- Added durable, structured stage start and completion records to
  `WharfLogger` without duplicating them on the console.
- Instrumented preparation, agent, verifier, and cleanup boundaries with full
  task and retry identity.
- Recorded explicit cancelled completions when cancellation interrupts a
  started preparation, agent, verifier, or cleanup stage.
- Kept verifier completion ahead of verifier-image cleanup so its duration
  remains comparable with earlier lifecycle measurements.
- Added logger regression coverage and Docker smoke-run assertions for paired
  events and non-negative durations.
- Documented the event fields and a single-file verifier timing query.

## Validation

- `dotnet format wharf.slnx --no-restore --include ...`: passed.
- Focused logger, progress, and retry tests: 23 passed.
- Docker integration: 5 passed.
- Full test suite: 134 passed.
- `dotnet format wharf.slnx --no-restore --verify-no-changes`: passed.
- `git diff --check`: passed.
