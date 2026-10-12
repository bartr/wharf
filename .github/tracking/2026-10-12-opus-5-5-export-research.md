# Opus 5.5 1.0.91 Export Research

## Scope

Export the retained `copilot-claude-opus-5.5-max-1.0.91/all-1` benchmark into
portable analytics, rebuild the analytics manifest, and add the result to the
root README ranking. The deleted incomplete Luna job is out of scope.

## Findings

1. The retained Opus run has 452 `result.json` files, which is the expected
   K=4 depth across 113 tasks. Its results use the legacy Harbor schema with
   `task_name`, `agent_info`, `agent_result`, `verifier_result`, and phase
   timestamps (`jobs/copilot-claude-opus-5.5-max-1.0.91/all-1/.../result.json`).
2. The legacy DeepSWE exporter is designed for this exact schema. It discovers
   `*__/result.json` trial directories, emits one row per trial, and retains
   reward, functional/regression counts, checksum, language, patch size,
   exception, cost, credits, agent duration, and trial duration
   (`../deep-swe/scripts/export_analytics.py:122-205`).
3. The exporter recovers older Copilot cost evidence from `session.shutdown`
   events when `cost.json` is absent, converting nano-AIU to credits and USD
   (`../deep-swe/scripts/export_analytics.py:56-112`).
4. The Wharf-native exporter expects Wharf run evidence and is documented for
   retained Wharf runs (`README.md:81-113`). Using it on a legacy Harbor job
   would mix evidence contracts unnecessarily.
5. The README ranking defines PASS@1 as passing attempts divided by the fixed
   planned attempt universe and defines Cost/test over costed trials only
   (`README.md:28-35`, `README.md:53-57`).
6. The reporting manifest is rebuilt strictly from JSONL files and records
   trials, tasks, scored rows, rows with cost, total known cost, and file size
   (`../deep-swe/scripts/build_data_manifest.py:18-64`).

## Risks

- Missing cost evidence must remain null; it must not be imputed or included as
  zero in Cost/test.
- Trial identities must be unique and exactly four per task.
- The README table must be reranked by the measured PASS@1 rather than appending
  the result at an arbitrary position.
- The current Wharf data directory and the canonical DeepSWE data directory
  both contain reporting exports. The new arm should be written to both, with
  each directory's manifest rebuilt from its own JSONL contents.

## Recommended approach

Run the canonical legacy exporter against the retained Wharf job root for each
analytics destination, rebuild both manifests, compute the result directly from
the exported rows, and update only the Wharf root README ranking. Validate row
count, unique trial identity, task coverage, K depth, reward totals, cost
conversion and coverage, duration coverage, checksums, and both manifest
entries.

## Not taking

- Do not restore, export, or score the deleted incomplete Luna job.
- Do not alter raw job evidence.
- Do not impute missing cost or duration values.
- Do not change scoring definitions or unrelated documentation.
