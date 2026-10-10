---
title: Effective job naming plan
description: Ordered implementation plan for model-aware smoke paths and effective CLI versions
---

## Tasks

1. [x] Update run naming and returned path metadata.
   * Target `src/wharf/Run/RunService.cs`.
   * Prepare the Copilot invocation before constructing the job directory.
   * Use `CopilotInvocation.EffectiveVersion` after successful preparation.
   * Preserve `configuration.CliVersion` as the setup-failure fallback.
   * Add the exact job and attempt directories to `RunResult`.
   * Exit when successful runs can expose a name such as
     `copilot-hydrafusion-max-1.0.91`.

2. [x] Remove smoke's reconstructed default path.
   * Target `src/wharf/Smoke/SmokeService.cs`.
   * Locate the trial below the job and attempt directories returned by
     `RunService`.
   * Exit when HydraFusion smoke selection does not inspect a Luna directory.

3. [x] Add focused regression coverage.
   * Target `tests/wharf.Tests/RunServiceTests.cs` or the nearest existing run
     configuration test file.
   * Cover effective-version job naming and configured-version fallback.
   * Exit when both naming behaviors are deterministic without Docker access.

4. [x] Validate and record results.
   * Run the smallest relevant .NET test selection.
   * Run the project build if the focused tests do not compile all changed
     production surfaces.
   * Record truthful outcomes in the changes and review artifacts.

## Parking Lot

* Existing directories named with `latest` remain unchanged.
* A future schema revision could persist requested and effective CLI versions
  separately in run summaries.
