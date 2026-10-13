# Claude Opus 5.5 Enterprise Export Changes

## Source

- Job: `enterprise/jobs/copilot-claude-opus-5.5-default-1.0.91/all-1`
- Arm: `copilot-claude-opus-5.5-default-1.0.91`
- Model: `claude-opus-5.5`
- Effort: `default`
- Copilot CLI: `1.0.91`

## Export

- Exported 56 unique logical trials across 14 tasks.
- Retained exactly four attempts per task.
- Rebuilt `enterprise/data/manifest.json` from nine JSONL arms.

## Results

- Passing trials: 52 / 56
- PASS@1: 92.86%
- P2P regression failures: 3
- Cost coverage: 56 / 56
- Total cost: $26.8327
- Cost per costed trial: $0.4792
- Average cost per task: $1.9166
- Mean agent duration: 180.8 seconds
- Mean trial duration: 261.9 seconds
- Exception-category trials: 0

## Validation

- Unique logical-trial identities: 56 / 56
- Task coverage: 14
- K depth: exactly 4 for every task
- Functional and regression measurements: 56 / 56
- Agent and trial duration coverage: 56 / 56
- Manifest totals: 9 arms and 504 logical trials
