# Latest WharfSWE analytics refresh

## Research

Reuse the canonical export/manifest contract in `docs/ANALYTICS.md:7-43`
and prior verified 1.0.113 export workflow. The read-only inventory finds five
new complete `all-1` runs: Codex Sol/Luna 0.169.0 and Copilot Sol/Luna/HydraFusion
1.0.114. Each has 56 terminal logical trials, 14 tasks, K=4/N=56.
Copilot uses P=16; Codex uses P=12. The Claude Code arm still has only
47/56 terminal results and is excluded, not repaired or exported.

`scripts/plot-wharf-swe-pass-at-1.py:196` summarizes every JSONL as Copilot.
Limit that Copilot-specific chart input to the existing `copilot-` arm naming
convention so Codex exports do not break or contaminate revision plots.
Canonical Codex billing costs remain null; prior API estimates are not bills.

## Plan

1. [x] Export five complete arms with explicit provider/model/version/effort.
2. [x] Rebuild only the WharfSWE manifest. Reconcile all new rows with raw
   reward, test, checksum, cost, patch, retry, and duration evidence.
3. [x] Limit revision plots to Copilot exports and regenerate three SVGs.
   Validate all plotted metrics, including revision 114.
4. [x] Update README comparison, full sorted table, coverage, date, and
   provider/cost caveats. Validate every table row against the manifest.
5. [x] Record changes/review, verify whitespace and scope. No commits,
   benchmark execution, evidence mutation, or DeepSWE changes.
