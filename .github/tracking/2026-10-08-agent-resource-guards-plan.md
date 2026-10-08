# Agent resource guards plan

1. [x] Add agent Docker command safeguards.
   - Target: `src/wharf/Agent/AgentExecution.cs`
   - Add `--memory 4g`, `--memory-swap 4g`, and `--pids-limit 512`.
   - Add `NPM_CONFIG_MANAGE_PACKAGE_MANAGER_VERSIONS=false`.
   - Exit: generated agent arguments contain all safeguards while retaining
     UID, network, mounts, and credential behavior.

2. [x] Add regression tests for the command contract.
   - Target: `tests/wharf.Tests/AgentExecutionTests.cs`
   - Assert exact Docker limits and pnpm setting.
   - Exit: focused agent execution tests pass.

3. [x] Make Ctrl-C cleanup unconditional after cancellation notification.
   - Target: `src/wharf/WharfApp.cs`
   - Use `try/finally` so cleanup runs if cancellation callbacks throw.
   - Exit: normal cancellation behavior is preserved and cleanup cannot be
     skipped by `CancellationTokenSource.Cancel`.

4. [x] Document the agent safety contract.
   - Target: `docs/wharf.md`
   - Record the limits and pnpm automatic-version-management override.
   - Exit: operators can understand the default containment behavior.

5. [x] Validate the implementation.
   - Run focused tests, the full suite, and the build in named tmux sessions.
   - Run a bounded Docker check proving the arktype project no longer starts a
     recursive pnpm installer under Wharf's home layout.
   - Exit: all checks pass and results are recorded in the changes/review
     artifacts.

