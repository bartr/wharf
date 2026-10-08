# Run artifact cleanup plan

1. [x] **Add explicit workspace retention configuration**
   - Targets: `src/wharf/Configuration/RunConfiguration.cs`,
     `src/wharf/Configuration/RunConfigurationRenderer.cs`,
     `src/wharf/Command/CommandTree.cs`
   - Add `retainWorkspaces` with flag-over-environment-over-manifest-over-default
     precedence and a default of false.
   - Exit: dry-run rendering exposes the effective value and
     `--retain-workspaces` opts into retention.

2. [x] **Persist the aggregate log with its run**
   - Targets: `src/wharf/Logging/WharfLogger.cs`,
     `src/wharf/Command/CommandTree.cs`, `src/wharf/Run/RunService.cs`
   - Buffer records until `RunService` selects `all-N`, then use
     `all-N/run.jsonl` unless the operator supplied `--log-file`.
   - Exit: the initial `started` record and all lifecycle records are retained
     at the selected path without changing dry-run side effects.

3. [x] **Remove terminal workspace state safely**
   - Target: `src/wharf/Run/RunService.cs`
   - After patch, agent, Copilot, verifier, and result evidence are available,
     remove the primary workspace and any Copilot infrastructure-retry
     workspaces unless retention is enabled.
   - Record cleanup success or failure in `result.json`; do not leave a path
     that claims a deleted workspace exists.
   - Ensure whole-trial retry archives do not recreate stale workspace paths.
   - Remove each verifier image tag immediately after that verifier finishes or
     fails, without pruning derived agent images or Docker build cache.
   - Exit: normal and retried trials preserve durable evidence while removing
     disposable workspace trees and run-scoped verifier image tags.

4. [x] **Add regression coverage and documentation**
   - Targets: focused configuration, logger, command, run retry tests and
     `docs/wharf.md`.
   - Exit: tests cover precedence, log attachment and buffering, cleanup,
     retained workspaces, and retry archives.

5. [x] **Validate and clean the completed N=32 run**
   - Run focused tests, full tests, build/format checks, and `git diff --check`.
   - Move the existing `wharf-run.jsonl` to the completed `all-1/run.jsonl`.
   - Delete only the 113 resolved `workspace` directories in that run and
     verify the resulting size, evidence inventory, and free space.
