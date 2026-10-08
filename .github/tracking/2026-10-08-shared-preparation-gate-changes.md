# Shared preparation gate changes

## Configuration

- Replaced manifest `b`, record member `B`, CLI `--b`, and environment
  `B`/`WHARF_B` with `p`, `P`, `--p`, and `P`/`WHARF_P`.
- Kept the default at 4 and the existing cap at N.
- Removed compatibility aliases; strict manifests reject legacy `b`.
- Renamed profile `--build-limit` and build recommendation output to
  `--preparation-limit` and P.
- Set the development version to `0.23.0`.

## Shared gate

- Added typed agent, agent-retry, and verifier preparation operations.
- Added synchronized waiting and active counters with monotonic event sequence
  numbers.
- Added durable `preparation-gate` transitions for waiting, acquired,
  released, and cancelled states.
- Acquisition events include wait duration; every event includes capacity,
  active occupancy, and queue depth.
- Preserved permit release when work or telemetry fails and explicit waiter
  accounting when acquisition is cancelled.

## Verifier boundary

- Split `PristineVerifierService` into `PrepareAsync` and
  `VerifyPreparedAsync`, while retaining composed `VerifyAsync`.
- Preparation validates inputs, creates the pristine workspace, enforces
  workspace separation, and applies the retained patch.
- Execution runs commands and retains command, patch, metrics, and atomic trial
  evidence with unchanged timeout and exit 137/143 classification.
- `RunService` now holds P across verifier image construction and verifier
  preparation, then releases it before invoking `VerifyPreparedAsync`.
- Existing verifier-image cleanup still surrounds both phases.

## Documentation

- Updated current README, requirements, PRD, requirements ledger, repo memory,
  and session records to describe P and the permit-release boundary.
- Historical tracking artifacts remain unchanged.

## Validation

- Expanded focused suite: 55 passed, 0 failed.
- Focused preparation-gate suite after the release-ordering fix: 4 passed,
  0 failed.
- Full suite: 153 passed, 0 failed in 148.4 seconds.
- Formatting verification, requirements-ledger parsing, and
  `git diff --check` passed.
- Active source, tests, README, and normative documentation contain no stale
  `.B`, `B:`, `--b`, `WHARF_B`, `--build-limit`, or build-recommendation
  surfaces.
- The compiled CLI accepts `--p`, renders `p`, and rejects `--b` with exit 1.
- `wharf profile --preparation-limit 12` renders `P=12`.
- A blocking verifier test proves verifier command execution does not retain a
  P permit and concurrent agent preparation can proceed.

The first full-suite run exposed a release-ordering race in telemetry: releasing
the semaphore before decrementing active occupancy allowed a newly awakened
waiter to report `active=2` at capacity 1. The gate now decrements occupancy and
creates the release event before making the permit available. The focused and
full suites pass with the corrected ordering.
