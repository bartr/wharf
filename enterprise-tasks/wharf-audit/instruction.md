The Wharf 0.14.0 release has the preceding architecture but does not include
this release boundary. Implement the 0.14.0 to 0.15.0 change for Implement audit scoring and completeness.

Use the historical public contract in the target release as the compatibility
boundary. Add the production types, validation, and behavior for the
Implement audit scoring and completeness. Preserve existing behavior, do not modify existing test files, and
retain explicit errors rather than silent fallbacks.

The implementation should match the target release's public API and tests for
this boundary. Keep unrelated workflow artifacts out of the change.

Preserve the historical public contract in the `Wharf.Audit` namespace.
Expose `AuditScorer`, `AuditRun`, `AuditReport`, and `MeasurementState`, while
preserving the existing `Wharf.Configuration.RunConfiguration` and
`Wharf.Configuration.WharfConfiguration` types.

IMPORTANT: Do not commit changes. The evaluator captures the working-tree
patch after implementation.
