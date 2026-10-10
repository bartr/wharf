# Session 23 qualification progress telemetry research

## Scope and evidence

- `ProgressService` currently treats a record as completed only when a
  `status=complete` line also has a successful result, then reconstructs its
  terminal set by combining successful and failed records
  (`src/wharf/Run/ProgressService.cs:114-147, 190-215`). Terminal lifecycle and
  grader coverage are therefore not represented independently.
- Preparation telemetry contains operation, transition, capacity, active,
  waiting, and acquisition wait, but no trial identity or K-wave context
  (`src/wharf/Run/PreparationGate.cs:12-19, 42-124`).
- The logger persists preparation events as structured JSON, so extending the
  event contract is sufficient to make queue identity and wait evidence
  durable (`src/wharf/Logging/WharfLogger.cs:146-177`).
- `RunService` owns all three preparation-gate call sites and already has the
  run number, task ID, logical attempt, trial retry, verifier retry, and K
  values needed to supply context (`src/wharf/Run/RunService.cs:125-127,
  224-287, 334-347, 490-535`).
- Interactive formatting lives in `CommandTree`, which this task explicitly
  excludes. The core can expose queued and aggregate values without changing
  that formatter or Reporting contracts.
- Session requirements require terminal completion independent from grader
  coverage and queue counts derived from structured transitions
  (`docs/session-log.md:905-922`).

## Recommended approach

1. Split terminal lifecycle from grader-result coverage in `ProgressService`.
   Count every final `status=complete` record as terminal, record verifier
   result coverage separately, and classify missing results by the retained
   error type or final result.
2. Add an immutable preparation context to each gate operation. Persist that
   context on every lifecycle event and retain wait duration on both acquired
   and cancelled terminal queue transitions.
3. Replay structured preparation transitions in `ProgressService` to derive
   the current queued count and operation/K wait aggregates. Do not infer this
   count from human status messages.
4. Make only the three necessary `RunService` call-site changes, preserving
   the existing agent-completion and verifier-only retry behavior.

## Risks and exclusions

- Older logs without structured preparation events cannot provide queued or
  wait aggregates; they remain readable with zero queue telemetry.
- Older terminal logs without a verifier completion are intentionally
  classified as missing grader coverage.
- Reporting/Audit analytics, `CommandTree`, documentation, and generated
  artifacts are not changed.

