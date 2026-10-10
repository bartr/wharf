---
title: Effective job naming changes
description: Implementation record for model-aware smoke paths and effective CLI versions
---

## Changes

* Moved Copilot invocation preparation ahead of job directory creation.
* Named successful job directories with
  `CopilotInvocation.EffectiveVersion`.
* Extended `RunResult` with the exact job and attempt directories.
* Changed smoke selection to use those returned directories.
* Added focused tests for effective-version naming and setup-failure fallback.

## Validation

The focused .NET test selection passed:

```text
Passed: 4
Failed: 0
Skipped: 0
```

The selection covered `RunServiceTests` and `SmokeServiceTests`. The test build
compiled the changed production and test projects for .NET 10.
