---
title: Resume Progress Reconciliation Plan
description: Implementation plan for correcting active progress totals across retained-run resume boundaries
author: Wharf maintainers
ms.date: 2026-10-11
ms.topic: concept
---

## Scope

Correct `wharf progress` so an append-only resumed run does not count stale
pre-resume lifecycle occupancy alongside current resumed activity.

The in-progress Opus run and its retained log are read-only validation inputs.
No test or implementation step may invoke a benchmark, smoke execution,
Docker operation, or command that writes to the run directory.

## Implementation tasks

1. Add resume-boundary reconciliation to
   [`ProgressService.cs`](../../src/wharf/Run/ProgressService.cs).

   * Detect only messages beginning with the exact `resume plan ` prefix.
   * Remove nonterminal progress records at the boundary.
   * Preserve terminal records and cumulative retry, cost, result, error, and
     wait aggregates.
   * Clear active preparation queue state.
   * Clear run-ordinal full-name mappings before run numbers are reused.
   * Continue processing subsequent records as the current execution epoch.

   Exit criteria:

   * One resume boundary supersedes all stale nonterminal occupancy.
   * Historical terminal metrics remain unchanged.
   * Multiple boundaries reconcile repeatedly without duplicating activity.

2. Add synthetic regression coverage to
   [`ProgressServiceTests.cs`](../../tests/wharf.Tests/ProgressServiceTests.cs).

   * Cover one completed historical trial, one stale active trial, a resume
     boundary, and one current active trial.
   * Assert one completed and one running trial.
   * Assert the current stage and full task name are reported.
   * Cover a second resume boundary and verify that only the newest active
     epoch contributes to occupancy.
   * Assert historical retry and cost aggregates remain intact.

   Exit criteria:

   * Tests fail against the pre-fix reader and pass after reconciliation.
   * Tests use temporary JSONL files only.

3. Run isolated validation in a named tmux session.

   * Run only the `ProgressServiceTests` test class.
   * Do not run the full suite because it includes a smoke execution.
   * Do not run `wharf run`, `wharf resume`, or any Docker command.
   * Read the live Opus log only after tests pass and compare the reported
     running count with the resume plan size.

   Exit criteria:

   * All `ProgressServiceTests` pass.
   * The live read-only command reports no more than 78 active trials for the
     current resume.
   * The live run log and trial directories have not been modified by
     validation.

4. Record implementation and validation results in
   `.github/tracking/2026-10-11-resume-progress-reconciliation-changes.md`.

   Exit criteria:

   * The artifact lists exact files changed, isolated test results, and the
     read-only live progress result.

## Deferred work

* Replacing human-readable lifecycle status records with structured logical
  attempt identifiers
* Changing completed-result, retry, cost, or wait aggregation semantics
* Modifying resume scheduling or the active Opus execution
* Running smoke or end-to-end execution while the Opus resume is active

## Plan exit

The implementation is limited to progress-log interpretation and synthetic
unit coverage. Validation cannot create containers, mutate benchmark evidence,
or compete with the running Opus workload.
