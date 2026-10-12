# Opus 5.5 1.0.91 Export Changes

## Completed

- Exported 452 logical trials from the retained `all-1` job to
  `data/copilot-claude-opus-5.5-max-1.0.91.jsonl`.
- Copied the identical portable export to
  `../deep-swe/data/copilot-claude-opus-5.5-max-1.0.91.jsonl`.
- Rebuilt both reporting manifests from their respective JSONL directories.
- Added the result to the root README ranking at rank 10.

## Measured result

- PASS@1: 309/452 = 68.3628%.
- Task accuracy: 92/113 tasks solved at least once.
- Known cost: $4,474.04805 across 392/452 trials.
- Cost evidence coverage: 86.73%.
- Cost per costed trial: $11.413388.
- Exceptions: 60 agent timeouts and 2 verifier timeouts.

## Validation

- 452 rows, 113 tasks, and 452 unique `(task_id, trial)` identities.
- Every task has exactly four retained attempts.
- The Wharf and DeepSWE JSONL files have the same SHA-256:
  `aa1871126f217553d7380f54e7909789e59f9de98cbbd11413f3a528dfb5fc58`.
- All rows retain agent and trial duration, task checksum, language, and patch
  size.
- 448/452 rows retain functional and regression test counts.
- All cost-bearing rows satisfy `cost_usd == credits * 0.01`.
- Both manifest entries match 452 trials, 113 tasks, 392 cost-bearing rows,
  $4,474.05 known cost, and 344,446 JSONL bytes.

## Tooling note

The checked-in `./wharf` launcher predates the documented `analytics` command,
so its manifest invocation failed without modifying analytics data. The
reporting-only canonical manifest builder was then used directly for both data
directories.
