The Wharf 0.10.0 release verifies trials but lacks deterministic scheduling.
Implement the 0.11.0 scheduler slice.

Admit attempts in stable attempt-ID order under positive concurrency and trial
limits. Emit lifecycle events through the supplied sink. Bank successful
logical-attempt IDs immediately so later duplicate attempts are skipped.
Cancellation must prevent new admissions, mark pending work cancelled, and
preserve terminal results from work already completed. Return deterministic
started-attempt, banked-attempt, and lifecycle-count summaries.

Preserve the historical public contract in the `Wharf.Scheduler` namespace.
Expose `DeterministicScheduler`, `SchedulerResult`, `ITrialRunner`, and
`ISchedulerEventSink`, while preserving the existing
`Wharf.Configuration.RunConfiguration` and
`Wharf.Configuration.WharfConfiguration` types.

Do not modify existing tests or commit changes. The evaluator captures the working-tree
patch after implementation.
