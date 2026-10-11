The Wharf 0.12.0 release has the preceding architecture but does not include
this release boundary. Implement the 0.12.0 to 0.13.0 change for Implement interruption recovery planning.

Use the historical public contract in the target release as the compatibility
boundary. Add the production types, validation, and behavior for the
Implement interruption recovery planning. Preserve existing behavior, do not modify existing test files, and
retain explicit errors rather than silent fallbacks.

The implementation should match the target release's public API and tests for
this boundary. Keep unrelated workflow artifacts out of the change.

Preserve the historical public contract in the `Wharf.Recovery` namespace.
Expose `RunRecoveryPlanner`, `RecoveryPlan`, `RecoveryAttempt`, and
`RecoveryException`, while preserving the existing
`Wharf.Configuration.RunConfiguration` and
`Wharf.Configuration.WharfConfiguration` types.

IMPORTANT: Do not commit changes. The evaluator captures the working-tree
patch after implementation.
