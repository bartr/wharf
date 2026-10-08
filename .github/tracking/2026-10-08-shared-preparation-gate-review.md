# Shared preparation gate review

## Scope reviewed

- Breaking B-to-P configuration, CLI, environment, profile, and documentation
  surfaces.
- Preparation-gate permit lifetime, telemetry ordering, cancellation, and
  callback-failure behavior.
- Verifier preparation/execution separation and verifier-image cleanup.
- Focused and full regression coverage.

## Findings

The initial full-suite run found one telemetry race. `PreparationGate` released
its semaphore before decrementing active occupancy, so an awakened waiter could
increment the counter first and emit an impossible occupancy above capacity.
The release path now decrements occupancy and sequences the release event while
holding the counter lock, then releases the semaphore before leaving that lock.
The regression test and full suite pass after the fix.

No remaining correctness issues were found.

## Boundary confirmation

- Agent image and workspace preparation run under P.
- Agent infrastructure-retry preparation runs under P.
- Verifier image creation, pristine workspace creation, and patch application
  run under P.
- `VerifyPreparedAsync`, including every verifier command, runs only after
  `PreparationGate.ExecuteAsync` has returned and released its permit.
- Verifier-image cleanup remains in the surrounding `finally`, covering
  preparation and execution failures.
- Cancellation while waiting records a cancelled transition without changing
  active occupancy or leaking a permit.

## Evidence

- Expanded focused suite: 55 passed.
- Post-fix preparation-gate suite: 4 passed.
- Full suite: 153 passed in 148.4 seconds.
- Formatting, JSONL parsing, and diff whitespace checks passed.
- Compiled CLI accepts `--p`, rejects `--b`, and profile output reports P.
- Active-surface search found no legacy B configuration names.

## Decision

Approved for commit on `0.23.0`. The cold benchmark and default-P tuning remain
separate follow-up work.
