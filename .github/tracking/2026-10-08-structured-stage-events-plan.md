# Structured stage events plan

1. [x] Add an additive structured stage-event API to `WharfLogger`.
   - Persist full task identity, logical attempt, trial retry, stage,
     transition, result, and duration without changing existing status logs.
   - Keep structured events out of normal console output.

2. [x] Instrument production trial stages.
   - Emit preparation, agent, verifier, and cleanup start/completion events.
   - Emit failed, skipped, cancelled, and successful completions from the same
     stage boundary.
   - Keep verifier duration consistent and emit completion before image cleanup.

3. [x] Add regression and integration coverage.
   - Validate structured JSON fields and full task IDs.
   - Prove a Docker smoke run emits paired stage events with non-negative
     durations while existing progress parsing still succeeds.

4. [x] Validate and document.
   - Run focused tests, Docker integration, formatting, and diff checks.
   - Document the stable fields and query semantics.
