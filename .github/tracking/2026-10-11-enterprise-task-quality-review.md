---
description: "Review of enterprise-task instruction leakage and evaluator behavior revisions"
author: "Wharf"
ms.date: 2026-10-11
ms.topic: "concept"
---

## Verdict

Accepted.

The revisions address the research finding: copying disclosed identifiers is no
longer enough to pass the feature evaluator. Public names remain in prompts
where callers need historical compatibility, while hidden coverage now
exercises observable behavior.

## Review checks

* Instructions state outcomes and invariants without embedding expected test
  code or assertion sequences.
* The image-pull evaluator accepts explicit invalid-option rejection at any
  lifecycle point allowed by its revised instruction.
* Packaging still discloses its three public script entrypoints, but its tests
  now execute syntax and invalid-input behavior rather than searching only for
  the prompt's command fragments.
* Verifier still discloses its public records and interfaces because exact
  signatures are compatibility requirements. Its evaluator now verifies
  sequential stop behavior, evidence persistence, early empty-patch rejection,
  workspace separation, and credential-name validation.
* Generic release-boundary tasks now describe the historical behavior needed
  to implement them and test that behavior directly.
* Existing pass-to-pass coverage remains green for every baseline.

## Validation outcome

The final named-`tmux` validation completed with exit code 0:

* 43 feature tests passed
* 14 compatibility tests passed
* 0 failed
* 0 skipped

Repository checks also confirmed clean patch whitespace, valid unified-diff
hunk counts, clean patch application, and unchanged grader test IDs.

## Residual limitations

The evaluators remain intentionally compact, with three feature tests for most
tasks and four for verifier. They now provide meaningful behavioral signal but
do not exhaustively prove each release slice. A new controlled model run is
required before comparing the revised benchmark score with the original 13/14
result.
