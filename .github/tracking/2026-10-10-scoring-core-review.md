# Scoring core review

## Scope validation

- PASS@1 now uses logical attempts, while task pass-at-least-once is separately
  named and serialized.
- Enterprise Score remains an expected-task-equal mean of per-task attempt means;
  its 80-point coverage and 20-point pass contributions are separately exposed.
- Analytics export emits exactly one row per native logical-trial directory and
  coalesces retained whole-trial and infrastructure retry evidence.
- Manifest generation reads only top-level portable JSONL files.
- Native CLI commands expose both export and manifest generation.
- An All-1 export reconciles to 452 logical trials, 113 tasks, 447 costed
  trials, and $539.70 known cost.

## Validation

- Focused and integration tests executed successfully.
- Changed-file whitespace formatting check passed.
- Diff whitespace check passed.
- Full repository validation passed: 195 tests, 0 failed, 0 skipped.

## Follow-ups

None required for this scope.
