# External agent home changes

## Implementation

- Derived a separate sibling home for every initial and infrastructure-retry
  agent workspace.
- Mounted the home at `/home/wharf` during workspace initialization, Copilot
  installation, agent execution, and ownership cleanup.
- Changed `HOME` and `XDG_CACHE_HOME` so neither path is below `/workspace`.
- Retained the pnpm automatic-version-management environment override as
  defense in depth.
- Extended normal cleanup to remove each derived home with its workspace.
- Updated agent argument and retry-cleanup regression tests.
- Documented the separate home as the primary protection against recursive
  pnpm self-install staging.

## Validation

- Focused test command:
  `dotnet test tests/wharf.Tests/wharf.Tests.csproj --filter
  'FullyQualifiedName~AgentExecutionTests|FullyQualifiedName~RunServiceRetryTests'
  --no-restore`
  - Result: 28 passed, 0 failed.
- Focused format check:
  `dotnet format wharf.slnx --verify-no-changes --no-restore --include
  src/wharf/Agent/AgentExecution.cs src/wharf/Run/RunService.cs
  tests/wharf.Tests/AgentExecutionTests.cs
  tests/wharf.Tests/RunServiceRetryTests.cs`
  - Result: passed.
- `git diff --check`
  - Result: passed.

## Koota qualification

- Started a K=1/N=1/B=1 run selecting only
  `koota-entity-snapshot-rollback` in tmux session `koota-home`.
- Confirmed separate live mounts:
  - trial workspace to `/workspace`
  - sibling `workspace-home` to `/home/wharf`
- Confirmed Koota's project-level
  `manage-package-manager-versions: true` remains effective.
- Confirmed zero recursive `pnpm add pnpm@10.28.1` processes.
- The first `pnpm -F @koota/core test run` completed without `EAGAIN`,
  process exhaustion, or runaway memory.
- The isolated benchmark was still running when this change was committed; its
  final verifier result remains a release-qualification follow-up.
