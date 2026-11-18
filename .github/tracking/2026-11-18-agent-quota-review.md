# Agent quota review

## Accepted

Build command:

`dotnet build tests/wharf.Tests/wharf.Tests.csproj --no-restore --verbosity quiet`

Focused tests ran foreground with pane-connected stdout/stderr in named tmux
`wharf-agent-quota-20261118-b`, using repository-local TMPDIR:

`dotnet test tests/wharf.Tests/wharf.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~AgentQuotaTests|FullyQualifiedName~AgentExecutionTests|FullyQualifiedName~ClaudeCliTests|FullyQualifiedName~CodexCliTests" --logger "console;verbosity=normal"`

Execution completed with `QUOTA_TEST_EXIT=0`, 114 passed, zero failed/skipped.
Tests cover live stdout and stderr notifications before child exit, exact stream
retention, provider shell log mirroring/exit-code preservation, reset normalization,
secret-free metadata, transient negatives, quota partial patches, no retry,
synthetic runner callbacks, paused starts, and retry starts.

Scoped `dotnet format whitespace wharf.slnx --no-restore --verify-no-changes`
and `git diff --check` both passed.

## Integration boundary

- `AgentStarting` may run twice for real execution: service admission and the
  actual Docker command boundary. Parent supplied an idempotent callback.
- Quota returns a failed summary with `ErrorType == AgentQuota`, not a thrown
  quota exception. Parent must prevent whole-trial retry and verifier acceptance
  of retained partial patches using this type.
- Live quota notification does not kill the active agent or discard evidence.
  The parent owns admission stop policy and durable batch state.
- Only structured provider error/subscription events are recognized. Generic
  plain-text diagnostics, transient rate limiting, and ordinary content are not.
- No paid agents, real benchmark jobs, batch storage, RunService, CLI, or README
  were modified by this implementation.
