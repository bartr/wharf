# 1.0 Release Review Research

## Scope

Current `0.25.0` HEAD is the replacement 1.0.0 candidate. The review covers
production runtime correctness, recovery, evidence integrity, cancellation,
Docker ownership, and effective configuration.

## Confirmed release blockers

1. `RunService.PrepareInvocation` ignores resolved credentials and always reads
   `~/.secrets/copilot_a` (`src/wharf/Run/RunService.cs:1132-1150`).
2. Validated phase timeout overrides are not consumed by agent build, agent,
   verifier build, or verifier command execution
   (`src/wharf/Run/RunService.cs:291,337,596,1049`).
3. Terminal `result.json` is written non-atomically, and recovery treats any
   existing result file as complete
   (`src/wharf/Run/RunService.cs:759`,
   `src/wharf/Recovery/RetainedRunResumePlanner.cs:95`).
4. Resume reloads the entire task root, so filtered runs cannot resume, and it
   reconstructs configuration with defaults instead of preserving the original
   effective policy (`src/wharf/Recovery/RetainedRunResumePlanner.cs:79-89`,
   `src/wharf/Command/CommandTree.cs:429-450`).
5. Ctrl+C immediately runs global Docker cleanup. Cleanup matches unrelated
   names containing `__`, and it races normal cancellation evidence capture
   (`src/wharf/Docker/DockerRunCleanup.cs:22-37`,
   `src/wharf/WharfApp.cs:16-27`).
6. File-backed pristine verifier workspaces are never deleted
   (`src/wharf/Verifier/VerifierExecution.cs:172-181`).
7. External cancellation of a host verifier does not terminate its process tree
   (`src/wharf/Verifier/VerifierExecution.cs:258-289`).

## Recommended approach

- Persist a secret-free, atomic run plan containing the effective configuration
  and exact selected task identities, with a legacy fallback for existing runs.
- Validate terminal result identity and status before completion; archive
  malformed results for rerun.
- Apply phase overrides ahead of task-specific and default timeout values.
- Route resolved credentials transiently through the existing Docker-process
  environment boundary without persisting or logging their values.
- Make Ctrl+C cancellation-only and restrict any explicit global cleanup to
  Wharf labels.
- Track pristine-workspace ownership and remove owned workspaces in `finally`.
- Kill and reap host verifier process trees on external cancellation.

## Out of scope

- Moving or recreating the existing `1.0.0` tag.
- Benchmark execution.
- Refactoring unrelated runtime architecture.
