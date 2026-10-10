# Scoring core research

## Findings

- `AuditScorer` currently derives `PassAt1` from first-attempt passing tasks divided
  by expected tasks, rather than passing observed logical attempts divided by all
  observed logical attempts (`src/wharf/Audit/AuditScorer.cs:25-29,68-71`).
- Enterprise scoring already averages attempt scores within a task and then gives
  every expected task equal weight, including missing tasks as zero
  (`src/wharf/Audit/AuditScorer.cs:32-63,71`). Its coverage and pass contributions
  are combined inside `AttemptScore` and are not exposed
  (`src/wharf/Audit/AuditScorer.cs:79-94`).
- Reporting prints `PassingTasks` as though it were a coverage metric, but that value
  currently means first attempts that passed, not tasks solved at least once
  (`src/wharf/Reporting/ReportRenderer.cs:23-27`).
- Native trial evidence is split across the trial `result.json`, `agent/result.json`,
  `agent/patch.diff`, `verifier/reward.json`, `verifier/trial-result.json`, and the
  attempt-level `run.jsonl` stage stream (`src/wharf/Run/RunService.cs:150-170,664-687`;
  `src/wharf/Agent/AgentExecution.cs:181-220`;
  `src/wharf/Verifier/VerifierExecution.cs:98-110,131-154`;
  `src/wharf/Logging/WharfLogger.cs:118-148,277-303`).
- The canonical DeepSWE portable row is one logical trial and retains identity,
  outcome/test counts, task checksum/language, patch size, exception type, cost,
  token/step/request usage, and agent/trial timing. The data manifest is derived only
  from JSONL exports and contains arm, trials, tasks, scored, cost coverage, total
  cost, and byte size (`../deep-swe/data/README.md:3-14,42-51`;
  `../deep-swe/scripts/build_data_manifest.py:35-56`).

## Recommended approach

Keep task-equal Enterprise aggregation intact while independently accumulating
trial-level PASS@1 and task pass-at-least-once. Return explicit Enterprise coverage
and pass components whose sum is the Enterprise Score. Add a reporting-layer
analytics model, a strict native evidence reader that coalesces trial retries into
one logical row, a deterministic JSONL exporter, and a manifest builder that reads
only exported JSONL.

## Not taken

- No command wiring or changes to execution, progress, logging, preparation, or run
  services.
- No benchmark execution or full-suite tests.
- No inference of absent cost, timing, tests, or usage data.
