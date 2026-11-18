# Completed-batch analytics refresh

## Research

Reuse the export workflow in `2026-11-18-wharfswe-analytics-plan.md`
and the canonical contract in `docs/ANALYTICS.md:7-43`.
Read-only inspection confirms 56 terminal results and zero pending slots for
Claude Code Opus, Codex Sol, and Codex Astra. Codex Luna is already complete.
All use 14 tasks, K=4/N=56/P=12 and their retained exact CLI versions.
No other new completed run directories are present.

`src/wharf/Reporting/RunEvidenceReader.cs:145-214` aggregates retained agent
executions, including whole-trial and infrastructure retries. Token sums alone
do not establish complete usage evidence, so estimates must also check every
retained execution. Rejected quota payloads are absent and must stay excluded.
`src/wharf/Reporting/DataManifestBuilder.cs:33-70` summarizes `cost_usd`;
API-equivalent estimates must not masquerade as billed subscription costs.
Add separate JSONL estimate fields and keep the canonical manifest's cost
coverage unchanged.

`scripts/plot-wharf-swe-pass-at-1.py:196-206` already selects only Copilot
exports and rejects duplicate model/revision identities. Regenerate and verify
the SVG inputs without changing that policy.

Standard USD rates per million tokens, checked 2026-11-18:
<https://developers.openai.com/api/docs/pricing.md>.

| Model | Input | Cached input | Output |
|---|---:|---:|---:|
| gpt-6-astra | 10 | 1 | 50 |
| gpt-6.1-sol | 2 | 0.10 | 10 |
| gpt-6-luna | 0.10 | 0.01 | 0.50 |

These are short-context rates (at most 272K input tokens per request).
Retained aggregate usage does not establish per-request context lengths.
Label estimates as this pricing scenario, not exact bills. Subtract cached
input from total input before pricing, and do not charge reasoning output twice.

## Plan

1. [x] Export completed Opus, Sol, Astra, and refresh Luna from retained evidence.
2. [x] Add a reproducible offline Codex estimate-enrichment script with focused
   tests for caching, retries, incomplete evidence, and invalid token accounting.
   Preserve canonical fields and billed-cost nulls.
3. [x] Enrich Codex JSONL, rebuild only the WharfSWE manifest, regenerate the
   three Copilot-only SVGs, and update README tables/coverage and pricing caveats.
4. [x] Validate task/checksum/K coverage, rewards/tests/patches/exceptions,
   actual agent durations, cost conversions/estimates, manifest entries,
   README tables, and Copilot-only SVG contents. Record results.

No benchmark agents, raw-evidence edits, DeepSWE changes, commits, or releases
are part of this refresh.
