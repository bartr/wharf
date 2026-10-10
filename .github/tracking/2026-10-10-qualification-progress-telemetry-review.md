# Session 23 qualification progress telemetry review

## Acceptance review

- Terminal count is driven by final `status=complete`, independent from
  verifier/grader coverage.
- Missing grader results are counted and classified using retained error type,
  then final result, then `Unknown`.
- Queue count is replayed only from structured waiting/acquired/cancelled/
  released events and queued runs are excluded from active occupancy.
- Preparation lifecycle records carry task, logical attempt, trial retry,
  operation retry, K wave, run identity, and durable wait milliseconds.
- Operation/K wait aggregates are exposed from `ProgressService`, progress
  JSON, and human progress output.
- Interactive output distinguishes queued, active, terminal, and missing-grader
  counts.
- All-2 confirmed the prior generic verifier fallback could rerun a completed
  agent. Prefix-based verifier routing now prevents that transition.
- Docker's concurrent-removal response is treated as an in-progress cleanup,
  and Wharf waits for container disappearance before image deletion.

## Validation outcome

- Focused tests executed successfully: 73 passed, 0 failed, 0 skipped.
- `git diff --check` passed.
- Full repository validation passed: 195 tests, 0 failed, 0 skipped.

## Follow-up

No implementation follow-up remains for this scope.
