# Retryable verifier termination changes

## Problem

Verifier command timeouts and externally killed Docker verifier containers
were returned as ordinary failed verifier results. They did not set a trial
error type, so the whole-trial retry predicate treated them like deterministic
reward-zero test failures.

## Change

- Persist raw command output, timing, patch, and `trial-result.json` first.
- Surface timed-out commands as `VerifierTimeout`.
- Surface exit codes 137 and 143 as `VerifierInfrastructure`.
- Keep ordinary nonzero verifier exits and reward-zero metrics as failed tests.
- Reuse the existing bounded whole-trial retry policy for the new exceptional
  error types.

## Validation

- Focused verifier, retry, task-loader, and scheduler tests: 32 passed.
- Formatting passed.
- `git diff --check` passed.
