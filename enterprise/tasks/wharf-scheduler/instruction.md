The Wharf 0.10.0 release has the preceding architecture but does not include
this release boundary. Implement the 0.10.0 to 0.11.0 change for Implement deterministic trial scheduling.

Use the historical public contract in the target release as the compatibility
boundary. Add the production types, validation, and behavior for the
Implement deterministic trial scheduling. Preserve existing behavior, do not modify existing test files, and
retain explicit errors rather than silent fallbacks.

The implementation should match the target release's public API and tests for
this boundary. Keep unrelated workflow artifacts out of the change.

Preserve the historical public contract in the `Wharf.Scheduler` namespace.
Expose `DeterministicScheduler`, `SchedulerResult`, `ITrialRunner`, and
`ISchedulerEventSink`, while preserving the existing
`Wharf.Configuration.RunConfiguration` and
`Wharf.Configuration.WharfConfiguration` types.

Use these historical public shapes:

- `ScheduledAttempt(string AttemptId, string LogicalAttemptId, string TaskId)`;
- `TrialExecutionResult(bool Succeeded, string? Error = null)`;
- `SchedulerLifecycle` exposes string constants including `Agent`,
  `Verifier`, and `Completed`;
- `SchedulerEvent(string AttemptId, string LogicalAttemptId, string Lifecycle,
  string? Detail = null)`;
- `SchedulerOptions(int MaximumConcurrentTrials, int MaximumTrials)` with
  `Validate()`;
- `ITrialRunner.RunAsync(ScheduledAttempt, Func<string, Task> transition,
  CancellationToken)`;
- `ISchedulerEventSink.RecordAsync(SchedulerEvent, CancellationToken)`;
- `DeterministicScheduler(ITrialRunner, ISchedulerEventSink)`; and
- `RunAsync(IReadOnlyList<ScheduledAttempt>, SchedulerOptions,
  CancellationToken cancellationToken = default)` returns a `SchedulerResult`
  with the exact shape
  `SchedulerResult(IReadOnlyList<ScheduledAttempt> StartedAttempts,
  IReadOnlySet<string> BankedLogicalAttempts, SchedulerCounts Counts)`;
  `SchedulerCounts` exposes `Pending`, `Preparing`, `Starting`, `Agent`,
  `Verifier`, `Completed`, `Errored`, and `Cancelled`.

Admit attempts in stable attempt-ID order within the configured limits. Bank a
logical attempt immediately after success. Later attempts with a banked logical
ID are skipped without invoking the runner but still reach a completed
lifecycle count.

IMPORTANT: Do not commit changes. The evaluator captures the working-tree
patch after implementation.
