# Opus 5 Default Enterprise Export Changes

## Changes

- Exported the completed 14-task K=4 Opus 5 Default run to
  `enterprise/data/copilot-claude-opus-5-default-1.0.91.jsonl`.
- Rebuilt the enterprise manifest with eight arms and 448 trials.
- Added Opus 5 to the enterprise summary and added Cost/run to the main table.

## Result

- PASS@1: 48/56 = 85.71%.
- P2P regression trials: 2.
- Cost evidence: 56/56 trials totaling $61.5417.
- Cost/run: $1.0990.
- Mean agent time: 230.7 seconds.
- Mean trial time: 268.1 seconds.

## Validation

- 56 unique logical-trial identities match the raw results.
- All 14 tasks have exactly four attempts.
- Functional, regression, cost, checksum, language, patch, and duration
  evidence is complete.
- Every cost row converts retained AI Credits at one cent per credit.
