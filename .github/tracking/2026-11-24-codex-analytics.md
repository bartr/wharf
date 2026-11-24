# Codex analytics refresh

## Research

- Six unexported completed arms exist: GPT-6.1 Sol and GPT-6 Luna on Codex
  0.170.0, 0.171.0, and 0.172.0. Each has 56 terminal results and a retained
  K=4/N=56/P=16/default-effort plan, with no whole-trial or verifier retries
  (`wharf-swe/jobs/codex-gpt-6.1-sol-default-0.170.0/all-1/run-plan.json:1`
  and the corresponding five new arms).
- Reuse the established canonical export/manifest contract
  (`docs/ANALYTICS.md:8`) and API-equivalent estimate enrichment
  (`scripts/estimate-wharf-swe-codex-costs.py:19`, `:90`).
- Billed costs stay null; separate estimates retain Standard pricing,
  cached-input discounts, and the existing pricing-check date/assumptions.
- User explicitly requested no README updates. Keep the README and
  Copilot-only SVGs unchanged; no raw evidence or runtime changes.

## Plan

1. [x] Export exactly six new arms with explicit Codex/model/version/default
   effort/subagent metadata, one row per retained logical trial.
2. [x] Apply existing estimate enrichment, rebuild only the WharfSWE manifest,
   and preserve all historical exports and manifest entries.
3. [x] Validate 56 unique trials/14 tasks/K=4 per arm, task checksums, rewards,
   test counts, token and cost evidence, timing, patch sizes, exceptions,
   estimate provenance, and each new manifest entry. Verify scope/whitespace.

## Changes and review

- Canonical export completed for all six arms: 336 additional logical trials.
  Existing enrichment added API-equivalent estimates and provenance.
  Manifest rebuild succeeded with 68 arms and 3808 logical trials.
- All 336 rows match retained task IDs/checksums, terminal/verifier results,
  functional/regression counts, patch byte counts, actual summed agent-command
  timing, stage-event trial timing, and token usage.
- Every new trial has complete token evidence and an API-equivalent estimate.
  Billed cost/credits remain null; new manifest arms correctly have zero
  billed-cost coverage. All new trials have no exception category.
- Reconciled each manifest entry's identity, row count, file size, task count,
  passes, P2P-error count, regression-adjusted score, and mean agent time.

| Model | Codex | Passes | P2P errors | Score | API estimate/trial | Avg agent time |
|---|---|---:|---:|---:|---:|---:|
| GPT-6.1 Sol | 0.170.0 | 53/56 | 0 | 94.64% | $0.1689 | 186.1 s |
| GPT-6.1 Sol | 0.171.0 | 55/56 | 0 | 98.21% | $0.1656 | 186.1 s |
| GPT-6.1 Sol | 0.172.0 | 54/56 | 1 | 96.18% | $0.1513 | 180.1 s |
| GPT-6 Luna | 0.170.0 | 38/56 | 12 | 64.86% | $0.0055 | 96.3 s |
| GPT-6 Luna | 0.171.0 | 34/56 | 16 | 56.71% | $0.0048 | 83.7 s |
| GPT-6 Luna | 0.172.0 | 38/56 | 18 | 63.36% | $0.0047 | 84.7 s |

- API estimates use the existing Standard pricing scenario checked
  2026-11-18; they are not subscription charges or retained billed costs.
- All 62 historical manifest entries and JSONL exports remain unchanged
  (JSONL verified byte-for-byte). README, SVG charts, DeepSWE data, and raw
  run evidence are unchanged, as requested.
- Validation passed: canonical export/manifest commands, existing estimate
  enrichment, direct retained-evidence reconciliation, exact Decimal
  pricing/score calculations, historical-data checks, and `git diff --check`.
  No runtime/build changes or additional test tooling; no follow-ups.
