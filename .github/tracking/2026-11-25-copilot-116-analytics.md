# Copilot 1.0.116 analytics refresh

## Research and plan

Reused the established canonical export/manifest workflow
(`docs/ANALYTICS.md:8`) and previous retained-evidence validation.
Discovery found three new completed `all-1` arms: GPT-6.1 Sol, GPT-6 Luna,
and HydraFusion, each with K=4/N=56/P=16 and default effort
(`wharf-swe/jobs/copilot-gpt-6.1-sol-default-1.0.116/all-1/run-plan.json:1`
and the corresponding Luna and HydraFusion plans).

1. [x] Export the three completed arms with explicit model, CLI version,
   effort, job, and subagent metadata.
2. [x] Rebuild the WharfSWE manifest without changing historical exports
   or Codex API-equivalent estimate fields.
3. [x] Reconcile each new row and manifest entry against retained evidence.
   Keep this data-only refresh separate from README/chart updates.

## Changes and review

Canonical export retained 168 additional logical trials. Manifest generation
completed with 71 arms and 3976 logical trials.

| Model | Passes | PASS@1 | P2P errors | Score | Cost/trial | Avg agent time |
|---|---:|---:|---:|---:|---:|---:|
| GPT-6.1 Sol | 56/56 | 100.00% | 0 | 100.00% | $0.1901 | 306.2 s |
| HydraFusion | 53/56 | 94.64% | 0 | 94.64% | $0.2594 | 391.2 s |
| GPT-6 Luna | 48/56 | 85.71% | 4 | 84.71% | $0.0150 | 227.8 s |

All 168 rows passed direct reconciliation of logical identity, 14 task
IDs/checksums with four attempts each, configuration, terminal status,
verifier reward and functional/regression counts, patch size, credits-to-USD
conversion, summed agent-command durations, and stage-event trial durations.
Each new arm has complete cost/test/timing evidence, no whole-trial or
verifier retries, and no exception categories.

Manifest validation checked file sizes, row/task counts, passes, regressions,
score, rounded total and per-costed-trial costs, and average agent duration.
All 68 historical manifest entries are unchanged; existing JSONL files,
including Codex estimate metadata, are byte-for-byte unchanged.

README, charts, raw run evidence, and DeepSWE data remain unchanged.
Canonical export/manifest commands and targeted data validation succeeded.
No runtime changes or build/test-suite execution were needed.

## Requested README follow-up

The user subsequently requested a README update for the new Copilot results.
Updated latest Copilot metrics and sources, added the three historical table
rows in the documented sort order, and regenerated all three Copilot-only
charts through revision 116. Existing Claude/Codex comparison rows remain
unchanged; the table explicitly distinguishes its 65 compared arms from all
71 exported arms.

Aggregate coverage now reports 3976 trials, 3472 retained-cost measurements
(87.32%), and 504 separate Codex estimates. Validated all 65 result rows,
latest comparison metrics, coverage, and exact passing-trial/cost/p90-duration
values at revision 116 in all three SVGs. Chart generation includes 55
Copilot runs across five models. `git diff --check` passed.
