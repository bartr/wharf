---
title: Enterprise data export changes
description: Exported files and validation evidence for completed enterprise runs
---

## Changes

* Exported seven completed K=4 `all-1` runs to canonical JSONL under
  `enterprise-data/`
* Rebuilt `enterprise-data/manifest.json` from the exported JSONL files
* Added `enterprise-data/summary.md` with PASS@1, task accuracy, enterprise
  score, regression, cost, and timing metrics
* Excluded the empty Gemini `all-2` directory

## Validation

`wharf analytics export` reported 56 trials and 14 tasks for each arm.
`wharf analytics manifest` reported seven arms and 392 trials.

The focused export validation confirmed for every arm:

* 56 rows and unique logical-trial identities
* 14 tasks with exactly four attempts each
* Reward totals equal retained verifier pass totals
* Functional, regression, checksum, language, cost, agent-duration, and
  trial-duration evidence is present for every row
* `cost_usd` equals retained AI Credits multiplied by $0.01
* Manifest byte, row, task, scored, and cost-coverage counts match each JSONL
  file
* No `all-2` export exists
