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

Pending.
