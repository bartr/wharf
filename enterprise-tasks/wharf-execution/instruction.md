The Wharf 0.11.0 release schedules logical attempts but lacks independent
phase execution policy. Implement the 0.12.0 execution slice.

Give build, agent, and verifier phases independent concurrency gates and
positive resource limits. Compute effective timeouts from phase defaults,
multipliers, and optional phase overrides. Retry only failures explicitly
classified as retryable infrastructure failures, cap retry counts, and retain
versioned evidence for every retry without changing logical-attempt identity.
Caller cancellation must propagate without being reclassified as a timeout or
retryable failure. Always release phase permits after success, failure, timeout,
or cancellation.

Preserve the historical public contract in the `Wharf.Execution` namespace.
Expose `PhaseExecutionService`, `PhaseExecutionResult<T>`,
`PhaseResourceLimits`, `PhaseTimeoutPolicy`, and
`InfrastructureRetryPolicy`, while preserving the existing
`Wharf.Configuration.RunConfiguration` and
`Wharf.Configuration.WharfConfiguration` types.

Do not modify existing tests or commit changes. The evaluator captures the working-tree
patch after implementation.
