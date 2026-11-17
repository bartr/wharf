# Copilot 1.0.113 WharfSWE analytics plan

## Research

Five completed Copilot 1.0.113 `all-1` runs have no JSONL export:
Claude Opus 5.5, GPT-6 Astra, GPT-6 Luna, GPT-6.1 Sol, and HydraFusion.
Each has 56 terminal results, K=4 across 14 tasks, N=56/P=12, default effort.
The Claude Code subscription-limited run is a different provider and remains
pending; do not export it.

Use the canonical `analytics export`/`analytics manifest` commands documented
in `docs/ANALYTICS.md:19,37`. Regenerate the three existing README-linked SVGs
using `scripts/plot-wharf-swe-pass-at-1.py`. Preserve all retained logical
trials and keep DeepSWE untouched.

## Plan

1. [x] Export five arms with explicit retained model, CLI 1.0.113, effort,
   agent identity and job; rebuild only the WharfSWE manifest.
2. [x] Validate unique trial identities, 14-task/K=4 coverage, checksums,
   reward/test totals, credit-to-dollar conversion, timing evidence and
   manifest metrics. Preserve existing exports and raw evidence.
3. [x] Update README coverage/results and latest comparison; retain the
   historical 1.0.112 comparison with a clear heading. Regenerate graphs.
4. [x] Validate README/manifest alignment, SVG data points, whitespace and
   artifact scope. Record results. No paid runs, installations, or commits.
