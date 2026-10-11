---
description: "Implemented enterprise-task instruction and evaluator quality revisions"
author: "Wharf"
ms.date: 2026-10-11
ms.topic: "reference"
---

## Changes

* Replaced generic release-transition language with behavioral contracts for
  audit, execution, host qualification, operations, recovery, reporting,
  scheduling, and smoke tasks.
* Clarified that image-pull retry options may reject invalid values during
  option construction, coordinator construction, or explicit validation.
* Reframed packaging requirements around observable Debian, checksum, SBOM,
  signing, privilege, retention, and rollback behavior rather than exact
  implementation strings.
* Retained exact type names and signatures only where they form the historical
  public compatibility boundary.
* Replaced reflection-only hidden checks across all 14 tasks with executable
  tests for scoring, retries, ordering, cancellation, persistence, validation,
  deterministic rendering, local reuse, evidence retention, script safety, and
  workspace isolation.
* Preserved every configured fail-to-pass test ID, so grader configuration did
  not need migration.

## Validation

All validation ran inside the named `tmux` session
`enterprise-task-validation`.

For each of the 14 tasks, validation:

1. copied the task's baseline source into an isolated workspace;
2. applied the retained reference solution;
3. applied the revised hidden-test patch;
4. restored dependencies;
5. built the test project in Release configuration;
6. executed the compiled feature-test assembly through VSTest; and
7. executed the configured pass-to-pass precedence test.

Results:

* 14 of 14 reference solutions built successfully.
* 43 of 43 revised feature tests passed.
* 14 of 14 pass-to-pass compatibility tests passed.
* All hidden test patches apply cleanly to their task baselines.
* Every patch hunk count is valid.
* Every feature test method still matches its grader node ID.
* No reflection-only `assembly.GetType` or generic public-method assertions
  remain in the hidden test patches.
