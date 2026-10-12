# Opus 5.5 1.0.91 Export Review

## Outcome

Accepted. The implementation satisfies the research recommendation and every
plan exit criterion.

## Contract review

- Export unit is one logical trial: 452 rows and 452 unique trial identities.
- Coverage is complete: 113 tasks with exactly four attempts per task.
- PASS@1 retains all planned attempts: 309/452 = 68.3628%.
- Task accuracy remains separate: 92/113 tasks passed at least once.
- Cost is not imputed: $4,474.04804995 across 392 cost-bearing rows, or
  $11.413388 per costed trial, with 86.73% evidence coverage.
- Every cost-bearing row converts credits at one cent per credit.
- Every row retains checksum, language, patch size, agent duration, and full
  trial duration. Functional and regression counts are present on 448 rows.
- Both exported JSONL files are byte-identical.
- Both manifest entries match the underlying export.
- The README row is rank 10 in descending PASS@1 order.
- The incomplete `copilot-gpt-5.6-luna-max-latest` job remains absent.

## Validation execution

- Analytics contract validator: passed.
- README rank and value validator: passed.
- `git diff --check` in Wharf and DeepSWE: passed.
- Build and unit tests: not run because this change contains generated
  analytics data, manifests, and documentation only.

## Follow-ups

None required.
