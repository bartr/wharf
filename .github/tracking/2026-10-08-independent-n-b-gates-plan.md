# Independent N and B Gates: Plan

Research source:
`.github/tracking/2026-10-08-independent-n-b-gates-research.md`

1. [ ] Extract agent workspace preparation.
   - Targets: `src/wharf/Agent/AgentContracts.cs`,
     `src/wharf/Agent/AgentExecution.cs`.
   - Add an injectable workspace-preparer boundary.
   - Move root `/app` copy, ownership handoff, home/cache creation, and pinned
     Copilot installation out of `DockerAgentRunner`.
   - Keep root initialization networkless and credential-free; perform CLI
     installation as UID/GID `1000:1000`.
   - Exit: `DockerAgentRunner` only starts the prepared agent as `1000:1000`.

2. [ ] Separate N trial concurrency from B preparation concurrency.
   - Target: `src/wharf/Run/RunService.cs`.
   - Use N as the asynchronous outer trial concurrency.
   - Apply a cancellation-aware B gate around image preparation plus workspace
     preparation.
   - Release B before starting the agent.
   - Preserve failure evidence and release permits on failure/cancellation.
   - Exit: `N=32 B=12` can reach 32 running agents while no more than 12
     preparations run concurrently.

3. [ ] Add regression coverage.
   - Targets: `tests/wharf.Tests/AgentExecutionTests.cs` and a focused
     preparation-gate test.
   - Verify root copy, ownership, non-root CLI installation, prepared agent
     command, explicit setup failures, and B concurrency.
   - Exit: tests fail against the coupled implementation and pass after the
     split.

4. [ ] Update operator documentation and record changes.
   - Target: `docs/wharf.md` and the changes artifact.
   - Clarify independent N/B behavior and what preparation includes.

5. [ ] Validate and review.
   - Run focused tests, full tests, and build in named tmux sessions.
   - Compare implementation with the research and record remaining operational
     qualification work.
