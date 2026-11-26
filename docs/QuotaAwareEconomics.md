# Quota Aware Economics

- Claude Code
  - $25 / month
  - Opus 5.5
    - 36 WharfSWE evaluations in one 5 hour quota
    - 20 WharfSWE evaluations used 54% of a 5 hour quota
    - Full WharfSWE run used 13% of the weekly quota in 2 sessions
- Codex
  - $25 / month
    - Astra 6
      - 16 WharfSWE evaluations / 5 hour quota
      - Full WharfSWE run used 55% of the weekly quota
  - $100 / month
    - No 5 hour quota, weekly only
    - Astra 6
      - Full WharfSWE run used 8% of weekly quota

## Before-and-after quota capture

For each Claude Code or Codex batch, check quota immediately before starting
and after in-flight work has finished. Record both readings, not just the
final percentage:

- Provider, plan price/currency/billing period, model, CLI version, run path,
  and batch invocation ID.
- Timestamp and timezone for each reading; short-window and weekly quota
  percentages, whether the display reports used or remaining, and reset times.
- Requested batch limit, actual started and accepted trials, passing trials,
  rejected executions, and elapsed time.
- Any quota reset, concurrent use outside Wharf, or plan/model change between
  readings. Retain unknowns explicitly if a reading was missed.

Retain a secret-free screenshot or transcription of the quota display with
the run's research evidence. Do not capture account identifiers or credentials.
Compare percentage-point changes only within the same quota window and
display basis; a reset or other usage prevents clean attribution to the batch.

Quota utilization is not a billed per-trial cost. Keep subscription charges,
CLI-reported USD telemetry, and API-equivalent estimates separate.
