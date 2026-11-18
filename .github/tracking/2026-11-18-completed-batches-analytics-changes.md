# Completed-batch analytics changes

Exported completed Claude Code Opus 2.1.326, Codex Sol 0.169.0, and Codex
Astra 0.169.0; refreshed Codex Luna 0.169.0 with the current exporter.
All have 56 terminal logical trials, 14 tasks, K=4/N=56/P=12.
No rejected quota execution is added to accepted-trial accounting.

Added `scripts/estimate-wharf-swe-codex-costs.py` and four focused Python
regression tests. Estimates sum retained execution usage, including retries,
validate cache accounting and exported token totals, and require complete
usage for the entire logical trial. Costs use the documented Standard
short-context pricing scenario, not inferred per-request context lengths.
`estimated_cost_usd` and `cost_estimate` retain explicit pricing provenance.
Canonical `cost_usd` remains null for Codex.

| Arm | Passes | P2P errors | Score | Retained USD/trial | API estimate/trial |
|---|---:|---:|---:|---:|---:|
| Claude Code Opus | 55/56 | 0 | 98.21% | $0.3582 | - |
| Codex Astra | 55/56 | 0 | 98.21% | unknown | $0.7185 |
| Codex Sol | 54/56 | 1 | 96.18% | unknown | $0.1454 |
| Codex Luna | 40/56 | 11 | 68.68% | unknown | $0.0051 |

Rebuilt the canonical WharfSWE manifest: 59 arms / 3,304 trials.
Retained USD-cost coverage is 3,136/3,304 (94.92%); separate Codex estimate
coverage is 168/168. Manifest billed-cost metrics do not consume estimates.

Regenerated all three revision graphs. They are byte-for-byte unchanged:
the existing generator already selects only Copilot exports. Each graph
contains 49 points across five models. README now explicitly defers
Claude Code/Codex graphs until additional runs exist, replaces obsolete
pending-fixture prose, and aligns all result tables and coverage figures.

Validation: four focused Python tests passed; the read-only raw-evidence,
manifest, README, and SVG checks passed. No .NET runtime code changed.
No benchmark agents, provider requests, raw job changes, or DeepSWE changes.
