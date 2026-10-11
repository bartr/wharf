---
title: Shared Copilot CLI Cache Plan
description: Ordered implementation plan for persistent linked Copilot CLI images
author: Wharf maintainers
ms.date: 2026-10-10
ms.topic: plan
---

## Tasks

1. [x] Extend agent image preparation with the effective Copilot version.
   * Target `src/wharf/Image/DockerAgentImagePreparer.cs`,
     `src/wharf/Run/RunService.cs`, and `src/wharf/Build/BuildService.cs`.
   * Preserve the existing no-version path used by `wharf build`.
   * Exit when run preparation passes the adapter-verified effective version
     without changing configuration precedence.

2. [x] Add persistent task environment, CLI payload, and composed agent caches.
   * Target `src/wharf/Image/DockerAgentImagePreparer.cs`.
   * Keep configured ECR images as authoritative environment parents.
   * Retain Dockerfile-backed task environments under deterministic local names.
   * Build one CLI payload image per version and platform.
   * Compose the payload with each task parent using `COPY --link`.
   * Coalesce identical in-flight cache builds.
   * Exit when cache images survive trial cleanup and only unique trial children
     are registered as Wharf-owned ephemeral images.

3. [x] Replace workspace npm installation with cached CLI validation.
   * Target `src/wharf/Agent/AgentExecution.cs`.
   * Use `/opt/wharf/copilot/node_modules/.bin` in the execution path.
   * Verify the cached package version exactly matches the invocation version.
   * Exit when workspace preparation performs no npm installation and fails
     explicitly for a missing or mismatched cached CLI.

4. [x] Add cache and workspace behavior tests.
   * Target `tests/wharf.Tests/DockerAgentImagePreparerTests.cs` and
     `tests/wharf.Tests/AgentExecutionTests.cs`.
   * Test deterministic cache names, linked-copy Dockerfiles, persistent cache
     ownership, exact version checks, and removal of workspace npm installation.
   * Exit when targeted tests cover both generated Dockerfile contracts and the
     workspace command contract.

5. [x] Update directly related operator documentation.
   * Target `docs/wharf.md`.
   * Describe ECR parent retention, versioned CLI cache images, linked
     composition, and unique trial-child cleanup.
   * Exit when the documented lifecycle matches the implementation.

6. [x] Validate the implementation.
   * Run targeted image and agent execution tests.
   * Run formatting verification and the release build.
   * Run `wharf smoke` twice inside a named tmux session to exercise cache
     creation and a warm cache hit.
   * Exit when both smoke executions complete, the second reuses the persistent
     cache, and evidence contains no credential value.

## Parking lot

* Cache pruning and size limits
* Public cache inspection commands
* Shared verifier image caching
* Generalized cache support for future non-Copilot adapters
