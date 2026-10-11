The Wharf 0.11.0 release has the preceding architecture but does not include
this release boundary. Implement the 0.11.0 to 0.12.0 change for Implement phase execution policies and retries.

Use the historical public contract in the target release as the compatibility
boundary. Add the production types, validation, and behavior for the
Implement phase execution policies and retries. Preserve existing behavior, do not modify existing test files, and
retain explicit errors rather than silent fallbacks.

The implementation should match the target release's public API and tests for
this boundary. Keep unrelated workflow artifacts out of the change.

Preserve the historical public contract in the `Wharf.Execution` namespace.
Expose `PhaseExecutionService`, `PhaseExecutionResult<T>`,
`PhaseResourceLimits`, `PhaseTimeoutPolicy`, and
`InfrastructureRetryPolicy`, while preserving the existing
`Wharf.Configuration.RunConfiguration` and
`Wharf.Configuration.WharfConfiguration` types.

Use these historical public shapes:

- `ExecutionPhase` has `Build`, `Agent`, and `Verifier` values;
- `PhaseResourceLimits(int Build, int Agent, int Verifier)` exposes
  `For(ExecutionPhase)` and `Validate()`;
- `PhaseTimeoutPolicy(TimeSpan Build, TimeSpan Agent, TimeSpan Verifier,
  double Multiplier = 1,
  IReadOnlyDictionary<ExecutionPhase, TimeSpan>? Overrides = null)` exposes
  `For(ExecutionPhase)` and `Validate()`;
- `InfrastructureRetryPolicy(int MaxRetries)` exposes `Validate()`;
- `PhaseExecutionService(PhaseResourceLimits, PhaseTimeoutPolicy,
  InfrastructureRetryPolicy)` implements `IAsyncDisposable`; and
- `ExecuteAsync<T>(string logicalAttemptId, ExecutionPhase,
  Func<CancellationToken, Task<T>>,
  Func<Exception, bool> isRetryableInfrastructureFailure,
  CancellationToken cancellationToken = default)` returns a
  `PhaseExecutionResult<T>` exposing `Value` and `InfrastructureRetries`.

Resource limits are independent per phase. Apply the timeout override first,
then the multiplier. Retry only failures accepted by the supplied predicate,
up to `MaxRetries`, without changing the logical attempt ID.

IMPORTANT: Do not commit changes. The evaluator captures the working-tree
patch after implementation.
