---
title: Shared Copilot CLI Cache Research
description: Verified design for composing a versioned Copilot CLI payload onto cached agent environments
author: Wharf maintainers
ms.date: 2026-10-10
ms.topic: concept
---

## Scope

Implement a persistent, versioned Copilot CLI image cache that retains configured
task ECR images as the task environment source, supports Dockerfile-backed smoke
tasks, composes the CLI with `COPY --link`, and removes the current per-container
npm installation.

## Findings

* ECR-backed tasks bypass their environment Dockerfile and build a unique child
  directly from `environment.docker_image`
  (`src/wharf/Image/DockerAgentImagePreparer.cs:51`).
* Dockerfile-backed tasks rewrite the supported Mars ECR reference to the local
  `mars-base:latest` alias before building
  (`src/wharf/Image/DockerAgentImagePreparer.cs:83`,
  `src/wharf/Image/DockerAgentImagePreparer.cs:174`).
* The smoke fixture has no configured environment image and therefore exercises
  the Dockerfile-backed path (`smoke-tasks/smoke-go/task.toml:33`).
* Every agent container currently installs the effective Copilot version into
  `/workspace/.wharf-copilot` during workspace preparation
  (`src/wharf/Agent/AgentExecution.cs:812`).
* Agent execution prepends that workspace-local installation to `PATH`
  (`src/wharf/Agent/AgentExecution.cs:406`).
* The adapter establishes an exact requested and effective three-part CLI
  version before image preparation begins
  (`src/wharf/Copilot/CopilotContracts.cs:89`).
* Generated trial images are registered for process cleanup and removed after
  trial evidence completes (`src/wharf/Run/RunService.cs:709`,
  `src/wharf/Docker/DockerRunCleanup.cs:11`).
* The run preparation gate already encloses image and workspace preparation
  (`src/wharf/Run/RunService.cs:221`).

## Measurements

On the current host with Copilot CLI `1.0.95`:

* A direct container-local npm installation took 26.51 seconds.
* Building the first task-derived CLI image took 66.63 seconds, including 49
  seconds to export and unpack the approximately 176 MB CLI layer.
* Inspecting a retained image took 0.21 seconds.
* The cache is therefore most valuable when retained across runs or reused by
  three or more attempts, depending on npm and Docker export variability.

## Recommended approach

1. Preserve configured ECR task images as authoritative environment parents.
2. Persist Dockerfile-backed task environments under content-derived local
   names so smoke and local tasks can use the same composition pipeline.
3. Build one CLI payload image per effective version, platform, and recipe.
   The final payload image contains only `/opt/wharf/copilot`.
4. Compose each task environment with the shared payload using
   `COPY --link --from=cli`.
5. Persist the composed task and CLI image under a key derived from the base
   image identity, CLI payload identity, and composition recipe.
6. Continue building a uniquely tagged child for each logical trial.
7. Register only the trial child for automatic cleanup.
8. Replace workspace npm installation with an exact cached-version check.
9. Persist cache hit and build evidence without credential values.

## Cache identity

The CLI payload name must include:

* Exact effective Copilot version
* Operating system and architecture
* CLI image recipe version

The composed agent cache key must include:

* Prepared task environment image identity
* CLI payload image identity
* Composition recipe version

Mutable source tags alone are not sufficient cache identities.

## Risks

* Concurrent attempts can race to create the same payload or composed image.
  Image preparation must coalesce identical in-flight builds.
* Cached images must not enter `DockerRunCleanup`, or normal trial cleanup will
  erase the persistent cache.
* A tag collision or manual retag could select an invalid CLI. Workspace
  preparation must verify the package version from the composed image.
* Dockerfile-backed task cache keys may over-invalidate if they use the broader
  task checksum. Over-invalidation is safe, while under-invalidation is not.
* The first cache population is slower than the current one-time npm install.
  Smoke validation must exercise both cache creation and reuse.

## Not taking

* Rebuilding ECR-backed task environments from Mars
* Caching verifier images
* Adding cache pruning or a public cache command
* Removing the unique logical-trial child image
* Supporting agent adapters other than Copilot in this change
