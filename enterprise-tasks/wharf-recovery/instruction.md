The Wharf 0.12.0 release has phase retries but lacks interruption recovery.
Implement the 0.13.0 recovery-planning slice.

Replay and validate retained events for the requested run. Exclude completed
logical attempts and deterministic failures, resume unfinished and explicitly
retryable attempts in stable attempt-ID order, and preserve infrastructure
retry counts. Inspect live resources without deleting them and report resources
that cannot be associated with a known trial as orphaned. Reject mixed-run or
malformed evidence explicitly.

Preserve the historical public contract in the `Wharf.Recovery` namespace.
Expose `RunRecoveryPlanner`, `RecoveryPlan`, `RecoveryAttempt`, and
`RecoveryException`, while preserving the existing
`Wharf.Configuration.RunConfiguration` and
`Wharf.Configuration.WharfConfiguration` types.

Do not modify existing tests or commit changes. The evaluator captures the working-tree
patch after implementation.
