# Independent N and B Gates: Changes

## Implementation

- Extracted Docker workspace copy, ownership handoff, and pinned Copilot CLI
  installation into `DockerAgentWorkspacePreparer`.
- Reduced `DockerAgentRunner` to running Copilot in an already prepared
  workspace as UID/GID `1000:1000`.
- Changed run orchestration to admit up to N asynchronous trials while applying
  a separate cancellation-aware B gate to image and workspace preparation.
- Added preparation lifecycle statuses for operational monitoring.
- Added focused tests for preparation commands, agent command separation,
  concurrency limiting, and permit release after failure.
- Updated operator documentation for independent N/B semantics.

## Validation

- Focused tests:
  `dotnet test tests/wharf.Tests/wharf.Tests.csproj --filter "FullyQualifiedName~AgentExecutionTests|FullyQualifiedName~PreparationGateTests"`
  passed 10/10 tests in the `wharf-gates-focused` tmux session.
- Full test project:
  `dotnet test tests/wharf.Tests/wharf.Tests.csproj` passed 95/95 tests in the
  `wharf-gates-full` tmux session.
- The full suite's Docker smoke path completed workspace preparation, ran the
  agent, and passed verification with pass@1=1, f2p=100%, and p2p=100%.
- `dotnet build src/wharf/wharf.csproj` succeeded in the same tmux session.
