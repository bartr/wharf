# User configuration review

## Decision: keep

The implementation meets the approved simplification: remove arbitrary per-run
input files while keeping useful operator defaults, explicit experiment choices,
strict failures and immutable retained policy.

Acceptance evidence is in [changes](2026-11-25-user-config-changes.md):
212 focused Release tests, successful Release build, changed-file formatting,
compiled command help, valid requirements ledger and clean whitespace.
Process-level tests verify the actual home-directory discovery path and that
malformed current defaults cannot redefine or block retained resume/batch preview.

The persisted effective-config schema remains unchanged. Historical source and
benchmark evidence are preserved; current docs record the breaking removal and
old session/release descriptions are marked superseded. No measured speed,
cost or quality improvement is claimed.

## Residual evidence and follow-up

- Full-repository formatting remains blocked by the unrelated, pre-existing
  IDE0008 at `src/wharf/Agent/AgentExecution.cs:1007`; route that cleanup to a
  separate maintenance task rather than changing it here.
- Paid live discovery was intentionally excluded. No live provider or release
  qualification is claimed for this offline configuration change.
- CLI/user-default convenience is implemented and regression-tested; whether it
  reduces operating friction remains a future user-observation question.

## Startup initialization follow-up review

Keep the explicit initialization requested after the initial review. Every entry
passes through WharfApp before parsing; default publication is atomic and
no-overwrite, while existing config validation remains command-specific.
Thus help and retained-policy commands preserve their existing output and
behavior apart from creating missing user config. No run state, resources or
quota are consumed by initialization.

226/226 focused Release tests, Release build and changed-file format checks
passed. Process tests verify actual missing-home discovery and default contents,
while concurrent/preservation/error checks cover the initialization boundary.
The initial compilation failure and subsequent successful rerun are recorded in
changes. No live provider or release qualification is claimed.
