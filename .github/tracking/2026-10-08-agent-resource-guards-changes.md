# Agent resource guards changes

## Implementation

- Agent Docker commands now use a 4 GiB memory limit, a matching
  memory-plus-swap limit that permits no additional swap, and a 512 PID limit.
- Agent containers set
  `NPM_CONFIG_MANAGE_PACKAGE_MANAGER_VERSIONS=false`, preventing pnpm from
  recursively staging itself below Wharf's in-workspace home.
- Ctrl-C cleanup now runs from a `finally` block after cancellation
  notification, so an exception from a cancellation callback cannot skip
  managed Docker cleanup.
- Agent command-contract tests and operator documentation cover the new
  defaults.

## Validation

- Focused `AgentExecutionTests`: 8 passed, 0 failed.
- Full `wharf.Tests` suite: 95 passed, 0 failed.
- `dotnet build --no-restore`: passed.
- Bounded arktype image check:
  - 4 GiB memory
  - no additional swap
  - 512 PIDs
  - Wharf-style in-project home
  - pnpm automatic version management disabled
  - result: `pnpm-safe`, exit 0.

