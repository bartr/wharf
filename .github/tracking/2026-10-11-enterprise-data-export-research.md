---
title: Enterprise data export research
description: Evidence and export approach for completed enterprise benchmark runs
---

## Scope

Export and summarize completed K=4 enterprise benchmark runs under
`enterprise-data/`. Exclude active or empty runs.

## Findings

* The canonical exporter writes one row per logical trial and preserves failed
  trials, test counts, costs, timing, patch size, exception category, and task
  checksum (`ANALYTICS.md`, lines 8-35).
* The portable manifest must be rebuilt from exported JSONL rather than from
  retained jobs (`ANALYTICS.md`, lines 29-35).
* Six non-HydraFusion `all-1` runs each contain all 56 expected trial results:
  Gemini 3.8 Flash, GPT-5.6 Luna, GPT-6 Astra, GPT-6 Luna, GPT-6.1 Sol, and
  MAI Code 1.1 Flash.
* The HydraFusion run is active and incomplete. Gemini `all-2` contains no
  trial results. Neither run qualifies for export.
* The run directory names identify CLI version `1.0.95`, effort `default`, and
  the effective model. The retained evidence does not declare subagent
  availability, so exports should retain the default `false` value.
* The analytics exporter rejects duplicate logical-trial identities and writes
  output atomically (`src/wharf/Reporting/AnalyticsExporter.cs`, lines 10-40).

## Recommended approach

Use `wharf analytics export` once for each of the six completed runs, preserving
the run arm as the JSONL name and `all-1` as the job. Rebuild
`enterprise-data/manifest.json`, then derive a combined Markdown summary from
the exported rows. Validate 56 unique trials, 14 tasks, K=4 depth, reward
totals, cost conversion, duration coverage, and manifest values for every arm.

## Excluded work

* Do not export or summarize the active HydraFusion run.
* Do not export the empty Gemini `all-2` directory.
* Do not alter retained job evidence or existing DeepSWE data exports.
