# Agent Workspace Permission Fix: Changes

## Implementation

- Added a separate, managed Docker workspace-initialization invocation that
  runs as root, copies `/app/.` to the host-backed workspace, and assigns the
  resulting tree to UID/GID `1000:1000`.
- Kept the Copilot agent invocation at UID/GID `1000:1000` and removed its
  direct `/app` copy.
- Added explicit initialization failure reporting with Docker stderr/stdout.
- Added regression tests for root initialization, ownership handoff, the
  non-root agent boundary, and initialization diagnostics.
- Documented the ownership handoff in `docs/wharf.md`.

## Validation

- Focused gate:
  `dotnet test tests/wharf.Tests/wharf.Tests.csproj --filter FullyQualifiedName~AgentExecutionTests`
  passed 7/7 tests in the `wharf-permission-tests` tmux session.
- Full gate:
  `dotnet test tests/wharf.Tests/wharf.Tests.csproj` passed 92/92 tests in the
  `wharf-permission-full` tmux session.
- The full suite's Docker smoke test exercised the real initialization and
  agent flow: the agent completed, the verifier passed with pass@1=1, f2p=100%,
  and p2p=100%.
- `dotnet build src/wharf/wharf.csproj` succeeded with no reported warnings or
  errors in the same tmux session.
