# Opus 5.5 1.0.91 Export Plan

1. [x] Export the retained `all-1` job with the canonical legacy analytics
   exporter.
   - Sources: `jobs/copilot-claude-opus-5.5-max-1.0.91/all-1`,
     `../deep-swe/scripts/export_analytics.py`.
   - Targets:
     `data/copilot-claude-opus-5.5-max-1.0.91.jsonl` and the matching file in
     `../deep-swe/data/`.
   - Exit criteria: both files contain the same 452 logical-trial rows.

2. [x] Rebuild both analytics manifests.
   - Targets: `data/manifest.json`, `../deep-swe/data/manifest.json`.
   - Exit criteria: each manifest contains an entry matching its generated
     JSONL's row count, task count, cost coverage, known cost, and byte size.

3. [x] Compute and validate the benchmark summary.
   - Validate 113 tasks, exactly K=4, unique `(task_id, trial)` identities,
     checksums and languages present, reward totals, functional/regression test
     evidence, patch sizes, exception categories, cost conversion, cost
     coverage, agent-duration coverage, and trial-duration coverage.
   - Exit criteria: PASS@1 uses all 452 planned trials and Cost/test uses only
     rows with retained cost.

4. [x] Update the root README ranking.
   - Target: `README.md`, current benchmark results table.
   - Exit criteria: add the measured Opus 5.5 1.0.91 result and renumber the
     table in descending PASS@1 order without changing metric definitions.

5. [x] Review the generated data and documentation against the research and
   plan.
   - Targets: both JSONL exports, both manifests, `README.md`.
   - Exit criteria: independent validation reports no contract mismatch and
     the deleted Luna job remains absent.
