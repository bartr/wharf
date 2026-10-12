---
title: Enterprise data export plan
description: Ordered export and validation steps for completed enterprise runs
---

## Plan

1. [ ] Export each completed `all-1` run to a separate canonical JSONL file
   under `enterprise-data/`.
   * Targets: six completed model arms identified in the research
   * Exit criteria: each file contains 56 rows with unique trial identities
2. [ ] Rebuild the portable analytics manifest from `enterprise-data/`.
   * Target: `enterprise-data/manifest.json`
   * Exit criteria: six entries match their JSONL row, task, score, and cost
     totals
3. [ ] Generate a combined benchmark summary.
   * Target: `enterprise-data/summary.md`
   * Exit criteria: each arm reports PASS@1, enterprise score, task accuracy,
     cost evidence, cost per costed trial, timing coverage, and exception
     counts
4. [ ] Validate the completed export against retained evidence.
   * Check 56 rows, 14 tasks, four attempts per task, reward totals, one-cent
     credit conversion, duration coverage, and unique trial IDs for every arm
   * Confirm HydraFusion and Gemini `all-2` are absent
5. [ ] Record the exported files and validation evidence.
   * Target:
     `.github/tracking/2026-10-11-enterprise-data-export-changes.md`
   * Exit criteria: commands and results are recorded truthfully

## Parking lot

* Export HydraFusion only after its run is complete.
* Decide whether Gemini `all-2` should be rerun or removed separately.
