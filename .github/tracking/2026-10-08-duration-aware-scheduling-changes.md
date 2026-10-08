# Duration-aware scheduling changes

## Source evidence

- Reference run:
  `../jobs/copilot-gpt-5.6-luna-max-1.0.91/all-1`
- Imported evidence: 452 completed Pier agent execution intervals covering 113
  tasks and four attempts per task.
- Aggregation: median agent execution duration in seconds.
- Output: `tasks/agent-duration-estimates.json`, schema `1.1`.

The checked-in importer validates complete task coverage, rejects non-positive
durations and unknown tasks, verifies language against each task manifest, and
produces deterministic JSON.

## Implementation

- Added strict loading of optional root-level agent duration estimates without
  changing Harbor task manifests or task source checksums. Each entry includes
  language, measured median duration, and a separate scheduling duration.
- Added measured and scheduling durations to the in-memory Harbor task
  contract.
- Added deterministic admission ordering:
  1. attempt number ascending;
  2. estimated agent duration descending;
  3. task ID ascending;
  4. attempt ID ascending.
- Applied the same ordering contract to the standalone scheduler.
- Expanded the production run path into all configured K attempts. Previously,
  it logged `K` and multiplied the planned count but executed only attempt 1.
- Kept K=1 evidence directory names unchanged. K>1 trials, images, containers,
  and infrastructure retries receive attempt-specific identities.

## Resulting leading estimates

| Task | Median Pier agent seconds |
| --- | ---: |
| dynamodb-toolbox-lazy-recursive-schemas | 5371.641 |
| effect-sse-httpapi-streaming | 4845.723 |
| wasmi-trap-coredumps | 3964.680 |
| pebble-durability-wait-apis | 3690.174 |
| valibot-recursive-schema-composition | 3470.356 |

These tasks now enter at the front of every K wave instead of becoming
alphabetical drain-tail work.

## Rust admission spacing

Rust tasks are resource-heavy even when their measured agent durations differ.
The importer keeps their true medians for ETA and assigns synthetic scheduling
durations that place the five Rust tasks at positions 1, 17, 33, 49, and 65 in
every K wave:

1. wasmi-trap-coredumps
2. oxvg-structural-selector-preservation
3. boa-hierarchical-evaluation-cancellation
4. fd-deterministic-multi-key-sorting
5. pest-character-class-coalescing

This avoids admitting the three longest Rust tasks together at positions 3, 8,
and 15 while still starting the longest Rust work immediately.

## Validation

- Importer regeneration: byte-for-byte stable.
- Imported coverage: 113 tasks from 452 Pier attempts.
- Focused task-loader and scheduler tests: 11/11 passed.
- Project build: passed.
- `git diff --check`: passed before review artifact creation.
