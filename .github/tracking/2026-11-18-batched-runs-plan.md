# Batched runs implementation plan

## Evidence reused and implementation decisions

The approved PRD v1.1 and requirements ledger are the behavioral source.
Existing run/resume/provider research in the source notes is reused.
Additional direct inspection confirms:

* `CommandTree.cs:212-425` resolves ordinary run settings; bounded continuation
  must branch before resolving ambient policy.
* `RunService.cs:166-221` executes actual parallel work and archives resumed
  payloads. Bound this path, not the standalone scheduler.
* `RetainedRunMetadata.cs:20-90` provides secret-free pinned configuration.
* `FileStateStore.cs:52-63` establishes exclusive filesystem writer locks.
* `ExitCode.cs:5-10` reserves codes 0-5. Quota pause will use code 6.

Use atomically replaced schema 1.0 `batches.json` with invocation history,
admitted/started/accepted IDs and durable rejected-execution tombstones.
Audit raw deletion under the nearest repository `.github/tracking/` (or
run-local `.github/tracking/` when outside a repository). Replay unfinished
deletions before admission, without mutation in planning/dry-run.
Writer locks cover actual run and resume execution, not read-only planning.

## Ordered tasks

1. [x] Add bounded-run planning/routing, positive B validation, pinned-policy
   continuation, read-only exact dry-run, and quota exit 6.
2. [x] Add versioned batch state, run writer ownership, atomic stop/admission,
   audit-before-delete/recovery, and accepted/all-in accounting.
3. [x] Wire actual RunService selection, preparation/agent starts, quota
   drain, pristine invalidation, retry stopping, and final invocation status.
4. [x] Implement provider-specific quota classification/live callbacks and
   synthetic tests (delegated independent agent boundary).
5. [x] Ensure recovery and analytics respect rejection tombstones, preserving
   read-only access and immutable plans/logs.
6. [x] Add focused synthetic command/run/state/provider tests; validate in
   named tmux, build and formatting. Never execute/mutate actual fixtures.
7. [x] Update README, record changes/review, verify all requirements and scope.

Historical operational migration is already complete and is not rerun here.

Implementation discoveries within the approved plan:

* Persist the rejection tombstone at the live quota callback, not only after
  the agent returns, so a crash cannot archive or accept the partial execution.
* Refresh aggregate audit telemetry immediately before deletion, including on
  interrupted-deletion recovery. Credits and provider-reported USD stay distinct.
* Reconcile progress by full logical identity across invocation-local run
  numbers, and remove rejected slots from terminal/active counts. Targeted
  baseline regressions were fixed without changing baseline execution.
* Exclude the pre-existing live-model command test from offline validation.
  See the separate validation incident record; no historical recovery fixture
  was executed.
