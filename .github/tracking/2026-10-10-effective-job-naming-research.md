---
title: Effective job naming research
description: Evidence and recommendation for model-aware smoke paths and effective CLI versions
---

## Requirements

* Name successful run directories with the configured model.
* Replace the requested `latest` label with the CLI version resolved by the adapter.
* Make smoke selection use the directory created by the run rather than reconstructing it.
* Preserve the generated agent patch through verifier evidence.

## Findings

* `RunService.Execute` creates the job directory from `configuration.Model` and
  `configuration.CliVersion` before it prepares the Copilot invocation
  (`src/wharf/Run/RunService.cs:88-102`). A requested version of `latest`
  therefore remains in the path even though the adapter resolves it.
* `CopilotAdapter.PrepareAsync` exposes the resolved value as
  `CopilotInvocation.EffectiveVersion`
  (`src/wharf/Copilot/CopilotContracts.cs:97-105`).
* `SmokeService.WriteSmokeSelection` reconstructs the job directory from the
  default model and default version instead of using the run result
  (`src/wharf/Smoke/SmokeService.cs:108-114`). This searches for
  `copilot-gpt-5.6-luna-max-latest` after a HydraFusion run.
* The completed smoke run used Copilot CLI `1.0.91`, as recorded in its
  retained CLI log
  (`jobs/copilot-hydrafusion-max-latest/smoke-1/smoke-go__1d817027/agent/copilot-logs/process-1791644717820-61.log:1060`).
* The agent patch is retained identically at `agent/patch.diff`,
  `artifacts/model.patch`, and `verifier/patch.diff`. It changes
  `greet()` from `Hello` to `Hello world!`.

## Recommendation

Prepare the Copilot invocation before creating the job directory. Use the
invocation's effective version for successful setup, return the exact job and
attempt directories in `RunResult`, and have smoke selection consume those
paths directly. Retain the configured version only as an explicit fallback
when invocation preparation fails before an effective version is available.

## Risks

* Moving invocation preparation changes when the output directory is created.
  Configuration validation already occurs before `RunService.Execute`, and a
  failed invocation must still retain run evidence, so the fallback path must
  remain available.
* Extending `RunResult` changes its constructor, but current production usage
  is limited to command output and smoke selection.

## Out of Scope

* Renaming the already completed smoke directory
* Changing HydraFusion routing behavior
* Changing patch content or verifier semantics
