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

Use the target release's measurement and scoring shapes:

- `MeasurementState` has `Missing`, `Invalid`, `Partial`, `Zero`, and `Valid`;
- `Measurement<T>` is a value type constrained to structs, exposes `State` and
  `Value`, and provides `MissingValue`, `InvalidValue`, `PartialValue`,
  `ZeroValue`, and `ValidValue` factories;
- `AuditAttempt(string AttemptId, int AttemptNumber,
  Measurement<bool> Passed, Measurement<double> NewTestCoverage,
  Measurement<bool> ExistingTestRegression, Measurement<double> Cost,
  Measurement<long> InputTokens, Measurement<long> OutputTokens,
  Measurement<long> CacheTokens, Measurement<long> Requests,
  Measurement<long> Steps, Measurement<TimeSpan> AgentTime,
  Measurement<TimeSpan> TrialTime)`;
- `AuditTask(string TaskId, IReadOnlyList<AuditAttempt> Attempts)`;
- `AuditRun(string RunId, IReadOnlyList<string> ExpectedTaskIds,
  IReadOnlyList<AuditTask> ObservedTasks)`; and
- `AuditScorer.Score(AuditRun)` returns an `AuditReport` with `PassAt1`,
  `EnterpriseScore`, `ExistingTestRegression`, nullable aggregate `Cost`, and
  a `Completeness` value exposing `IsComplete` and measurement counts.

PASS@1 uses the lowest-numbered attempt for each expected task. Each attempt's
enterprise score is 80 times clamped new-test coverage plus 20 for a pass, and
is zero when existing tests regress. Missing or invalid cost evidence makes
aggregate cost null and completeness false.

Before finishing, build the solution and resolve all compilation errors in the
implementation.

IMPORTANT: Do not commit changes. The evaluator captures the working-tree
patch after implementation.
