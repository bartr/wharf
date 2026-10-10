# Session 23 qualification progress telemetry plan

1. [x] Extend preparation-gate events.
   - Files: `src/wharf/Run/PreparationGate.cs`,
     `tests/wharf.Tests/PreparationGateTests.cs`.
   - Add task, logical-attempt, retry, K, and run identity to every transition.
   - Retain wait duration for acquired and cancelled waits.
   - Exit: focused gate tests prove ordered contextual lifecycle and
     cancellation evidence.

2. [x] Persist the extended structured event.
   - Files: `src/wharf/Logging/WharfLogger.cs`,
     `tests/wharf.Tests/WharfLoggerTests.cs`.
   - Serialize every context field and durable wait value.
   - Exit: logger test parses the JSONL and verifies the full contract.

3. [x] Supply preparation context from execution.
   - File: `src/wharf/Run/RunService.cs`.
   - Update agent, infrastructure-retry, and verifier preparation call sites
     without altering retry decisions or agent completion.
   - Exit: project compiles and existing focused retry tests remain green.

4. [x] Reconcile progress from terminal and queue evidence.
   - Files: `src/wharf/Run/ProgressService.cs`,
     `tests/wharf.Tests/ProgressServiceTests.cs`.
   - Count terminal attempts independently from grader coverage.
   - Expose missing grader count/classification, structured queued count, and
     operation/K wait aggregates.
   - Exit: focused tests cover missing grader terminal attempts, distinct queue
     occupancy, cancellation, and aggregate grouping.

5. [x] Validate focused scope.
   - Run only PreparationGate, WharfLogger, ProgressService, and relevant
     RunService retry tests with a repository-local temporary directory.
   - Exit: all selected tests pass and no excluded files are modified by this
     task.

