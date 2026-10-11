---
description: "Implementation plan for reducing enterprise-task instruction leakage and strengthening evaluators"
author: "Wharf"
ms.date: 2026-10-11
ms.topic: "reference"
---

## Objective

Revise all 14 .NET enterprise tasks so their instructions define a solvable
compatibility and behavior contract without serving as a literal test oracle.
Make hidden evaluators exercise production behavior instead of relying
primarily on reflected type existence.

## Tasks

1. Update the eight release-boundary instruction/evaluator pairs.
   * Files: `enterprise-tasks/wharf-{audit,execution,host-profile,operations,recovery,reporting,scheduler,smoke}/instruction.md`
     and their corresponding `tests/test.patch` files
   * Add release-specific invariants derived from the historical implementation.
   * Keep public names that are legitimate compatibility requirements.
   * Replace generic "has any public method" assertions with deterministic
     operation, validation, serialization, ordering, or failure tests.
   * Exit criterion: each task tests at least one nontrivial observable behavior
     not copied verbatim into its instruction.

2. Update the six specialized instruction/evaluator pairs.
   * Files: `enterprise-tasks/wharf-{agent,copilot,derived-image,image-pull,packaging,verifier}/instruction.md`
     and their corresponding `tests/test.patch` files
   * Retain necessary API compatibility details while removing incidental test
     recipes.
   * Add behavioral checks for validation, normalization, deterministic keys,
     rate-limit classification, script failure safety, and verifier request
     isolation.
   * Make image-pull invalid-option testing accept explicit rejection at either
     construction or validation unless the lifecycle is stated as contractual.
   * Exit criterion: prompt-mirroring assertions are no longer the sole feature
     evidence.

3. Validate every revised task with its reference solution.
   * Build isolated temporary workspaces from each task environment.
   * Apply `solution/solution.patch` and `tests/test.patch`.
   * Run each task's normal offline test entrypoint inside a named `tmux`
     session.
   * Exit criterion: all 14 reference solutions earn binary reward 1, with no
     missing fail-to-pass node IDs.

4. Review the revisions.
   * Check instruction/test alignment, accidental solution disclosure, patch
     applicability, and repository whitespace.
   * Record validation and any residual benchmark limitations in a review
     artifact.
   * Exit criterion: review reports no unresolved blocker and identifies any
     intentionally retained public-contract disclosure.

## Out of scope

* Changing reference production solutions
* Running a new model benchmark
* Changing Wharf runtime code
* Hiding public API names that callers genuinely require for release
  compatibility
