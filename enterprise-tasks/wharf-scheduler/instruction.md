The Wharf 0.10.0 release has the preceding architecture but does not include
this release boundary. Implement the 0.10.0 to 0.11.0 change for Implement deterministic trial scheduling.

Use the historical public contract in the target release as the compatibility
boundary. Add the production types, validation, and behavior for the
Implement deterministic trial scheduling. Preserve existing behavior, do not modify existing test files, and
retain explicit errors rather than silent fallbacks.

The implementation should match the target release's public API and tests for
this boundary. Keep unrelated workflow artifacts out of the change.

IMPORTANT: Do not commit changes. The evaluator captures the working-tree
patch after implementation.
