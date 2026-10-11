---
title: Resume Command Research
description: Evidence and recommended design for exact logical-attempt resume
author: Wharf maintainers
ms.date: 2026-10-11
ms.topic: reference
---

## Findings

* `resume` is a placeholder command
  (`src/wharf/Command/CommandTree.cs:73`,
  `src/wharf/Command/CommandTree.cs:866`).
* `RunService` always expands every task through K and creates the next
  `all-N` directory (`src/wharf/Run/RunService.cs:103`,
  `src/wharf/Run/RunService.cs:122`,
  `src/wharf/Run/RunService.cs:1096`).
* Exact task and attempt identities already exist as `TaskAttempt` values
  (`src/wharf/Task/TaskSelector.cs:45`).
* A terminal logical trial is represented by `result.json`; the interrupted
  Opus run has 452 expected directories, 374 terminal results, and 78 missing
  results.
* The first run log records K, N, P, and admission order. The parent arm name
  records model, effort, and effective CLI version.
* Three unfinished trials contain prior whole-trial retry evidence. Resume must
  preserve that evidence and continue the current retry number.
* The existing state-event recovery planner is not connected to `RunService`
  and the benchmark run directory does not contain its state-store event
  stream (`src/wharf/Recovery/RecoveryContracts.cs:40`).

## Recommendation

Add a filesystem-backed resume planner for retained benchmark runs. It should
validate the run shape, infer immutable configuration, select only trial
directories without `result.json`, archive active partial evidence under
`resume-history`, and invoke `RunService` with an explicit existing directory
and exact `TaskAttempt` list.

Provide `--dry-run` before paid execution. Resume must append to the original
`run.jsonl`, retain completed results unchanged, and fail on malformed or
ambiguous evidence.

## Out of scope

* Resuming legacy runs whose arm name or initial plan record is missing
* Reconstructing unknown cost that was not retained before cancellation
* Automatically exporting analytics after execution
