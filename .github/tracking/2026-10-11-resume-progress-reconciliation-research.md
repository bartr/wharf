---
title: Resume Progress Reconciliation Research
description: Root cause and recommended correction for duplicated active totals after a retained run resumes
author: Wharf maintainers
ms.date: 2026-10-11
ms.topic: concept
---

## Problem

`wharf progress` reports 156 running trials during the Opus resume even though
the resumed execution contains 78 logical trials and Docker reports 78 active
Wharf containers.

The completed total remains correct at 374. The defect is isolated to active
lifecycle occupancy after appending resumed execution records to the original
run log.

## Evidence

* The original log ends with 78 nonterminal logical trials. Reading only the
  pre-resume records reports `running=78`, split across 67 agent, 6 verifier,
  and 5 cleanup records.
* Reading only records appended after the resume boundary reports
  `running=78`, all in the agent stage at the observation time.
* Reading the combined log reports `running=156`, which is the exact sum of the
  stale pre-resume occupancy and current resumed occupancy.
* Docker reported 78 active Wharf-owned agent or verifier containers at the
  same observation time.
* The retained log has an explicit resume boundary at
  [`run.jsonl`](/home/bartr/wharf/jobs/copilot-claude-opus-5.5-max-1.0.95/all-1/run.jsonl:14832).
  The boundary declares `runs=78`.
* Resumed execution intentionally appends a `resume plan` record to the
  original log in
  [`RunService.Execute`](/home/bartr/wharf/src/wharf/Run/RunService.cs:139).
* The progress reader keys lifecycle records by run ordinal and shortened task
  name in
  [`ProgressService.Read`](/home/bartr/wharf/src/wharf/Run/ProgressService.cs:136).
  Resume restarts run ordinals at 1 for its selected subset, so current records
  do not reconcile with the original nonterminal records.
* Active occupancy includes every nonterminal record left in the dictionary in
  [`ProgressService.Read`](/home/bartr/wharf/src/wharf/Run/ProgressService.cs:229).
  Nothing retires stale occupancy when a resume boundary is encountered.
* Full task names are also indexed by run ordinal in
  [`ProgressService.Read`](/home/bartr/wharf/src/wharf/Run/ProgressService.cs:129).
  Reused ordinals can therefore associate stale names with current activity
  until a new structured stage event replaces them.
* Existing progress tests cover lifecycle stages, preparation queues, retries,
  and full-name recovery, but do not cover an append-only log containing a
  resume boundary in
  [`ProgressServiceTests`](/home/bartr/wharf/tests/wharf.Tests/ProgressServiceTests.cs:1).

## Root cause

The progress reader treats an append-only resumed log as one uninterrupted
execution namespace. The original 78 cancelled or interrupted records remain
nonterminal. The resumed 78 records use a new 1-to-78 run ordinal namespace,
so they are added as separate records instead of replacing the stale
occupancy.

Terminal historical records are not affected and must remain available for
completed, pass-rate, error, retry, and cost totals.

## Recommended approach

Treat each `resume plan` message as an active-lifecycle reconciliation
boundary:

1. Retain all terminal progress records so cumulative completed metrics remain
   intact.
2. Remove every nonterminal progress record before processing resumed
   lifecycle messages. The resume planner schedules every unfinished logical
   attempt, so these records are superseded by the new execution epoch.
3. Clear active preparation queue state at the same boundary.
4. Clear the run-ordinal-to-full-task-name map so reused run numbers cannot
   display stale names.
5. Preserve historical retry, cost, completed-result, and preparation-wait
   aggregates.
6. Add a regression test with one terminal record, one stale nonterminal
   record, a resume boundary, and one resumed active record. The expected
   result is one completed and one running trial.
7. Add a multiple-resume test so each boundary supersedes only current
   nonterminal occupancy while retaining all terminal history.

This approach works with the existing retained Opus log and future appended
resume logs. It avoids relying on shortened task names as durable logical
identities.

## Alternatives not taken

* Re-key all records by shortened task name and attempt number. Truncated task
  names are not guaranteed unique, so this would replace one identity defect
  with another.
* Rewrite or truncate the original run log before resuming. The append-only
  evidence contract requires preserving history.
* Infer activity from Docker containers. Progress must remain a read-only
  evidence operation and must work after containers have exited.
* Mark cancellation records terminal. Cancellation does not complete a logical
  trial and would incorrectly inflate completed totals.

## Risks and constraints

* Resume boundary detection must use an exact prefix, not a loose substring,
  to avoid treating unrelated messages as lifecycle boundaries.
* The reconciliation must occur before stage, queue, and status processing for
  the first resumed record.
* Multiple resume boundaries must be idempotent.
* The fix must not change completed results, pass rate, retry count, retained
  cost, missing-grader totals, or historical wait aggregates.

## Research exit

The defect is fully reproduced and attributed to stale nonterminal occupancy
surviving an append-only resume boundary. The recommended fix is to reconcile
active-only state at each exact `resume plan` boundary while preserving
terminal and aggregate history.
