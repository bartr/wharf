# External agent home plan

Research source:
`.github/tracking/2026-10-08-koota-pnpm-recursion-research.md`

1. [x] Derive a per-agent home directory beside, rather than inside, each
   agent workspace.
   - Target: `src/wharf/Agent/AgentExecution.cs`
   - Mount the derived directory at `/home/wharf` for initialization, Copilot
     installation, agent execution, and ownership cleanup.
   - Set `HOME=/home/wharf` and
     `XDG_CACHE_HOME=/home/wharf/.cache`.
   - Exit criterion: no agent command has a home or cache path below
     `/workspace`.

2. [x] Preserve retry isolation and cleanup.
   - Target: `src/wharf/Run/RunService.cs`
   - Each infrastructure-retry workspace must naturally receive its own
     sibling home.
   - Delete derived homes whenever their workspaces are deleted.
   - Exit criterion: initial and retry home directories cannot survive normal
     trial cleanup.

3. [x] Add regression coverage.
   - Target: `tests/wharf.Tests/AgentExecutionTests.cs`
   - Assert the separate mount and environment for initialization, setup,
     execution, and cleanup.
   - Assert the pnpm safeguard remains present as defense in depth.
   - Target: existing RunService cleanup tests if present.
   - Exit criterion: focused tests prove the home is outside the project and
     cleanup removes it.

4. [x] Update operator documentation.
   - Target: `docs/wharf.md`
   - Explain the separate retained home and why pnpm staging must not occur
     below the repository.

5. [ ] Validate and run Koota.
   - Format and run the focused agent/run tests.
   - Build the current Wharf CLI.
   - Start a K=1 run selecting only
     `koota-entity-snapshot-rollback` in a named tmux session, with stdout and
     stderr attached directly to the pane.
   - Exit criterion: the task completes without a recursive
     `pnpm add pnpm@10.28.1` process tree and the verifier result is retained.

## Parking lot

- Seeding the new home from image-specific root caches is not required for the
  recursion fix and could copy unrelated image state.
- General runaway-process detection remains separate work; the existing PID
  limit remains containment.
