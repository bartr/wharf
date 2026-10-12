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

Use these historical public shapes:

- `RecoveryAttempt(string AttemptId, string LogicalAttemptId, string TrialId)`;
- `LiveResourceSnapshot(IReadOnlySet<string> ResourceIds)`;
- `ILiveResourceReconciler.InspectAsync(RunId, CancellationToken)`;
- `RecoveryPlan(RunId RunId,
  IReadOnlyList<RecoveryAttempt> AttemptsToResume,
  IReadOnlySet<string> CompletedLogicalAttempts,
  IReadOnlyDictionary<string, int> InfrastructureRetryCounts,
  IReadOnlySet<string> OrphanedResourceIds)`;
- `RunRecoveryPlanner(ILiveResourceReconciler? resources = null)`; and
- `PlanAsync(RunId runId, string eventsPath,
  IReadOnlyList<RecoveryAttempt> attempts,
  CancellationToken cancellationToken = default)`.

Resume eligible attempts in stable attempt-ID order. Exclude completed logical
attempts and deterministic terminal failures, preserve replayed infrastructure
retry counts, and report live resource IDs whose trial IDs are not represented
by the supplied attempts. An empty valid event stream and empty attempt list
produce an empty plan.

IMPORTANT: Do not commit changes. The evaluator captures the working-tree
patch after implementation.
