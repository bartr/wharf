# Verifier-only retry plan

Research source:
`.github/tracking/2026-10-08-verifier-only-retry-research.md`

## Fit check

Proceed. The change fits one focused RPI cycle if it stays inside retry
orchestration, progress parsing, retained evidence, tests, and directly related
documentation. New public retry settings, recovery changes, and active-run
migration remain out of scope.

## Tasks

1. [x] Separate verifier retry classification from whole-trial retry.
   - Targets:
     - `src/wharf/Run/RunService.cs`
     - `tests/wharf.Tests/RunServiceRetryTests.cs`
   - Add explicit verifier-retry classification for `VerifierTimeout` and
     `VerifierInfrastructure`.
   - Make the whole-trial predicate reject verifier-only failures while
     preserving agent-side retry behavior.
   - Exit criterion: focused tests prove each failure type has exactly one
     retry owner.

2. [x] Add narrow verifier evidence archival.
   - Targets:
     - `src/wharf/Run/RunService.cs`
     - `tests/wharf.Tests/RunServiceRetryTests.cs`
   - Move only the failed top-level `verifier/` directory to
     `verifier-retries/retry-N/`.
   - Preserve `agent/`, `artifacts/model.patch`, cost, and whole-trial evidence.
   - Fail explicitly if the destination already exists.
   - Exit criterion: a unit test proves verifier evidence moves while agent and
     artifact evidence remain in place.

3. [x] Execute verifier-only retries around the retained patch.
   - Target: `src/wharf/Run/RunService.cs`.
   - Add a bounded helper that retries only typed verifier infrastructure
     exceptions, invokes an archive/log callback before retry, honors configured
     backoff and cancellation, and returns the final result plus retry count.
   - Move verifier image preparation, pristine preparation, command execution,
     and image cleanup into the per-verifier-attempt callback.
   - Reuse the same retained patch and agent result; create a fresh verifier
     image identity, evidence directory, and pristine workspace each time.
   - Record `verifierRetry` in final `result.json`.
   - Exit criterion: timeout then success calls verifier execution twice, never
     invokes agent work, retains failed verifier evidence, and does not add
     agent cost.

4. [x] Expose verifier retries in progress.
   - Targets:
     - `src/wharf/Run/ProgressService.cs`
     - `tests/wharf.Tests/ProgressServiceTests.cs`
   - Count `status=verifier-retry` in `retries`.
   - Clear the superseded verifier error during retry and classify the active
     transition as verifier work.
   - Exit criterion: focused progress tests report one retry, active verifier
     occupancy, and no stale terminal error.

5. [x] Update directly related documentation and durable change record.
   - Targets:
     - `docs/wharf.md`
     - `.github/copilot-instructions.md`
     - `.github/tracking/2026-10-08-verifier-only-retry-changes.md`
   - State that verifier timeout/termination retries the retained patch in a new
     pristine verifier workspace without rerunning the agent.
   - Preserve whole-trial retry for agent-side failures.
   - Exit criterion: current normative text no longer says verifier failures
     rerun the whole trial.

6. [ ] Validate and review.
   - Run focused retry, verifier, and progress tests.
   - Run formatting verification and `git diff --check`.
   - Run the full suite in a named tmux session.
   - Write a review artifact confirming verifier retry does not invoke or charge
     agent execution and never holds P during commands.
   - Exit criterion: all checks pass and no verifier-only failure can reach the
     whole-trial retry path.

## Parking lot

- Separate public retry budgets for agent, verifier, and whole-trial failures.
- Resume an interrupted verifier-only retry from retained events.
- Show retry type breakdowns rather than only aggregate `retries=` in progress.
