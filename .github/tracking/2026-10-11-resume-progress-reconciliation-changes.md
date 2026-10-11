---
title: Resume Progress Reconciliation Changes
description: Implementation and constrained validation evidence for resumed-run progress totals
author: Wharf maintainers
ms.date: 2026-10-11
ms.topic: concept
---

## Changes

* Updated
  [`ProgressService.cs`](../../src/wharf/Run/ProgressService.cs) to treat an
  exact `resume plan ` message as an active-lifecycle reconciliation boundary.
* Removed stale nonterminal records at each boundary while retaining terminal
  history and cumulative aggregates.
* Cleared active preparation queues and run-ordinal full-name mappings when a
  resume begins.
* Included logical attempt numbers in progress record keys so reused run
  ordinals cannot overwrite a terminal record for another attempt.
* Added synthetic single-resume and multiple-resume regression cases to
  [`ProgressServiceTests.cs`](../../tests/wharf.Tests/ProgressServiceTests.cs).

## Validation

The filtered test class ran in the named `wharf-progress-tests` tmux session:

```text
dotnet test --filter FullyQualifiedName~ProgressServiceTests

Test summary: total: 20, failed: 0, succeeded: 20, skipped: 0
Build succeeded
```

The live Opus log was then parsed through the read-only `progress` command:

```text
completed=374 pass-rate=72.46% pass@=271 failed=83 errors=10 timeouts=10 retries=40 missing-grader=13
queued=0 running=78 building=0 agent=78 verifier=0 cleanup=0
avg-cost=11.76 total-cost=2645.52
```

The corrected active total matches the 78-attempt resume plan. Historical
completed, pass-rate, retry, missing-grader, and cost values remain unchanged.

## Isolation

Validation did not run the full test suite, smoke execution, Docker commands,
`wharf run`, or `wharf resume`. It did not write to the live Opus run
directory. Synthetic tests used temporary JSONL files only.
