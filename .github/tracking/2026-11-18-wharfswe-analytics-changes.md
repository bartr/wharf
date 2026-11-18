# WharfSWE analytics refresh changes

## Implementation

Exported five complete `all-1` runs through the canonical Release CLI:
Copilot Sol/Luna/HydraFusion 1.0.114 and Codex Sol/Luna 0.169.0.
Each arm has 56 logical trials, 14 tasks, K=4/N=56, default effort.
All five retain identical task checksums. Copilot P=16 differs from Codex P=12.

Rebuilt only `wharf-swe/data/manifest.json`: 57 arms, 3,192 trials,
3,080 retained costs (96.49% coverage), 19 trial exception categories.
Codex costs remain null, not API-equivalent estimates.

Limited the Copilot revision chart generator to `copilot-*.jsonl`.
Its strict per-arm validation remains unchanged. Regenerated the three
README-linked SVGs: 49 Copilot arms across five models, including three
revision-114 points. Codex is not placed on Copilot revision axes.

Updated README latest-run comparison, provider/configuration caveats, all
57 sorted results, coverage, and verification date. Preserved historical
1.0.113 comparison. Documented Codex usage-limit interruptions, missing
token evidence, null billing costs, and Luna whole-trial retry accounting.
The incomplete 47/56 Claude Code arm and all raw evidence remain untouched.

## Validation

Independent Python assertions reconciled all 280 new rows with terminal
results, task plans/checksums, verifier rewards/test counts, agent summaries,
patch byte counts, stage exception categories, and stage timestamp spans.
Agent durations include all whole-trial executions; retries do not add rows.
Copilot credit conversion uses Decimal arithmetic at $0.01 per credit.
All five manifest entries agree with independently computed metrics.

Validated all 57 README results and sorting against the manifest and all
147 plotted metric values against the JSONL exports (passing trials, mean
cost per costed trial, inclusive-p90 actual agent duration).
All 3,192 rows have reward/test/timing measurements; exactly 112 Codex rows
lack billed costs. Historical tracked JSONL exports match HEAD byte-for-byte.

A focused mixed-provider regression check invoked the actual chart entry
point with a temporary directory containing one real Copilot export and
one real Codex export. All three SVGs generated successfully, with only
the Copilot point. The pre-change input selector would reject the Codex
version. Temporary validation files were automatically removed.

No product/runtime source changed, so no .NET build or benchmark execution
was needed. `git diff --check` passed. No dependencies installed or commits
created.
