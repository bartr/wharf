# Remove unused commands: plan

Based on [research](2026-11-25-remove-unused-commands-research.md).

1. [x] Remove CommandTree registrations/handlers for status, stop, report and
   profile. Delete exclusive report/profile code/tests and status/stop services,
   contracts and tests; preserve notification behavior.
2. [x] Add focused process tests proving all four commands fail as unknown,
   disappear from help, initialize config as before and create no job evidence.
   Preserve resume/progress/analytics registrations and behavior.
3. [x] Update current source requirements, PRD/ledger and contributor guidance;
   mark historical session/release coverage superseded without rewriting
   historical evidence. Remove standalone report/auto-profile promises.
4. [x] Run focused offline Release tests and Release build/modified-file format
   in named tmux; exclude the known paid discovery test. Verify requirements
   JSONL integrity, whitespace and unchanged tasks/jobs/data.
5. [x] Inspect analytics export, evidence reader and manifest aggregation; report
   actual behavior and concrete gaps separately. Do not modify analytics.
6. [x] Record changes/review with truthful validation and evidence limits.

Exit criteria: retired CLI cannot execute, exclusive implementation is gone,
remaining tested boundaries work, docs reflect current surface and analytics
inspection has a source-backed conclusion. No speed/quality benefit or live
provider validation is inferred from offline tests.
