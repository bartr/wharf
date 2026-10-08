# Agent Workspace Permission Fix: Plan

Research source:
`.github/tracking/2026-10-08-agent-workspace-permissions-research.md`

## Tasks

1. [x] Add an explicit agent workspace initialization step.
   - Target: `src/wharf/Agent/AgentExecution.cs:105-180`.
   - Build a short-lived Docker invocation from the requested agent image that:
     - carries the managed-resource label;
     - runs as `0:0`;
     - mounts only the agent workspace at `/workspace`;
     - copies `/app/.` into `/workspace/`;
     - recursively changes `/workspace` ownership to `1000:1000`.
   - Wait for completion and capture stdout/stderr.
   - Throw `AgentExecutionException` with retained Docker diagnostics on any
     non-zero exit before starting Copilot.
   - Remove `cp -a /app/. /workspace/` from the UID 1000 agent command.
   - Exit criteria: root performs image-to-host materialization, and the agent
     process still runs as `1000:1000`.

2. [x] Make Docker command construction directly testable.
   - Targets: `src/wharf/Agent/AgentExecution.cs:105-180`,
     `tests/wharf.Tests/AgentExecutionTests.cs`.
   - Extract only the deterministic initialization and agent argument
     construction needed to assert the security/ownership contract; do not
     introduce a broad process abstraction.
   - Exit criteria: unit tests can prove ordering and argument contents without
     requiring Docker.

3. [x] Add regression coverage for the four-failure root cause.
   - Target: `tests/wharf.Tests/AgentExecutionTests.cs`.
   - Assert initialization uses root, copies `/app/.`, changes ownership to
     `1000:1000`, and retains managed labeling.
   - Assert the main agent invocation remains UID/GID `1000:1000` and no longer
     copies `/app` itself.
   - Assert root initialization failure is reported explicitly.
   - Exit criteria: tests fail against the pre-fix command flow and pass after
     implementation.

4. [x] Update directly related documentation.
   - Target: `docs/wharf.md`, agent execution section.
   - Document the root-copy/ownership handoff without implying that Copilot
     itself runs as root.
   - Exit criteria: operator-facing behavior and UID boundary are both clear.

5. [x] Validate the implementation.
   - Run the focused agent execution test class first.
   - Run the repository's test project and build in a named tmux session as
     required by `RUN_MONITORING.md`.
   - Exit criteria: focused tests, full tests, and build complete successfully;
     failures are recorded truthfully in the changes artifact.

## Parking Lot

- Verifier bind-mount permissions are out of scope because none of the four
  observed failures reached verification.
- General Docker process abstraction is deferred; this fix should remain local
  to agent execution.
- Benchmark re-execution is deferred unless unit/build validation reveals that
  a live Docker smoke test is necessary.
