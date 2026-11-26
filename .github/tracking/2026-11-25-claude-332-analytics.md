# Completed Claude Code 2.1.332 analytics

## Research and plan reused

Reused the canonical export contract in `docs/ANALYTICS.md:8` and the previous
completed-batch validation workflow. The retained
`wharf-swe/jobs/claude-claude-opus-5-5-default-2.1.332/all-1/run-plan.json:1`
identifies Claude Opus 5.5, default effort, and K=4/N=56/P=16.

1. [x] Confirm all 56 logical slots are terminal and the final batch is completed.
2. [x] Export the new arm and rebuild only the WharfSWE manifest.
3. [x] Reconcile every row and manifest metric; preserve prior exports.

## Changes and results

Exported `wharf-swe/data/claude-claude-opus-5-5-default-2.1.332.jsonl`
with explicit Claude CLI, version, model, effort, subagent, and job metadata.

| Metric | Result |
| --- | ---: |
| Logical trials / tasks / K | 56 / 14 / 4 |
| Passing trials / PASS@1 | 48/56 / 85.71% |
| P2P-error trials | 4 |
| Regression-adjusted score | 84.71% |
| Retained USD telemetry | $26.011176 |
| Cost coverage | 56/56 |
| Cost per costed trial | $0.4645 |
| Average agent-command duration | 120.0 seconds |

All eight batch invocations reconcile to 56 distinct accepted logical slots.
The final invocation accepted the remaining 20 slots and finished `completed`
with zero pending. No quota-rejection records or agent exceptions are present.
Eight ordinary failed trials remain in the denominator.

These USD values are retained Claude usage telemetry, not a measured allocation
of subscription fees or proof of quota-aware economic savings. Credits remain
null; no credits-to-USD conversion was applied to dollar telemetry.

## Validation and boundaries

Canonical export and manifest commands succeeded. Direct evidence checks
validated every trial's identity, task/checksum/language, K depth, reward,
functional/regression counts, patch size, costs, tokens, agent duration, and
stage-event elapsed span. No whole-trial or agent-infrastructure retry evidence
was found. Functional/regression totals are 172/56.

An initial independent check incorrectly treated cache tokens as read-only.
Inspection of the established aggregation contract confirmed read-plus-write
accounting; corrected validation passed without changing the export.

All 74 preceding JSONL files are byte-for-byte unchanged, and all 74 preceding
manifest entries are unchanged. The rebuilt WharfSWE manifest contains 75 arms,
4,200 trials, and 3,640 trials with retained USD evidence (86.67%).
README, charts, DeepSWE data, runtime code, and raw jobs were not changed.
No benchmark agents or test suites were launched.
