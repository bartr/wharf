# Remove unused commands: review

## Decision: keep

The approved simplification is implemented. Four commands are no longer
registered, return parser errors instead of placeholder success/failure, and
have no exclusive runtime modules left. Tests verify actual process behavior
and preserved config initialization without run evidence.

The [changes record](2026-11-25-remove-unused-commands-changes.md) retains
129 passing focused Release tests, build and changed-file formatting results.
Progress, resume/batching, analytics, smoke, notifications and cleanup behavior
are covered by the focused suite. Passing offline tests is not live provider
validation or release qualification.

Current docs expose progress and analytics, Ctrl+C cancellation and manual
N/P selection. Historical records remain explicitly historical. No task
payloads, retained runs or exports were rewritten.

## Follow-ups

- **Plan:** analytics hardening against the four synthetic reproductions in
  [the inspection](2026-11-25-analytics-inspection-research.md). Useful existing
  functionality should not be confused with fully validated input/evidence.
- **Separate documentation maintenance:** pre-existing ledger OQ-002 shape and
  aggregate counts do not agree. JSON syntax, stable IDs/references and changed
  claim provenance pass; no complete-schema validation claim is made.

Removing unused surfaces reduces complexity by design. No measured performance,
accuracy, productivity gain or historical score correction is claimed.
