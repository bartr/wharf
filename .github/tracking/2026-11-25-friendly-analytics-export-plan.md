# Friendly analytics export: plan

Based on [research](2026-11-25-friendly-analytics-export-research.md).

1. [x] Add atomic no-overwrite creation, shared terminal/arm/log evidence helpers,
   nullable unknown subagent evidence, and (job, trial) identity validation.
2. [x] Implement deterministic jobs/all-N discovery, native/legacy inference,
   complete-universe and batch-state checks, arm grouping, output resolution,
   explicit skip reasons, existing-file skips and --recompute replacement.
3. [x] Wire optional CLI arguments with ./jobs default and output-directory/
   recompute flags; preserve explicit single-arm overrides and reject conflicts.
4. [x] Add focused tests for multiple jobs/arms, incompleteness and active state,
   legacy evidence, unknown subagents, preserved checksums without task payloads,
   existing exports/recompute/races, and CLI defaults.
5. [x] Update operator/docs/source/PRD/ledger contracts together without changing
   historical records, task payloads or benchmark exports.
6. [x] Run focused offline tests, Release build and changed-file formatting in
   named tmux. Run compiled discovery against real jobs with scratch output only;
   verify logical counts, identity, costs/timing, skips and byte-preserved source.
7. [x] Record changes/review, clean scratch output, and leave uncommitted.

No accuracy/latency benefit is claimed. General semantic validation and legacy
reward admissibility remain the earlier inspection's separate follow-ups.
