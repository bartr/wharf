---
title: ECR Smoke Base Research
description: Research for removing the local Mars image dependency from smoke
author: Wharf maintainers
ms.date: 2026-10-10
ms.topic: reference
---

## Findings

* The smoke agent Dockerfile already declares
  `public.ecr.aws/x8v8d7g8/mars-base:latest`
  (`smoke-tasks/smoke-go/environment/Dockerfile:1`).
* The smoke verifier Dockerfile declares the same ECR image and already builds
  from that reference without substitution
  (`smoke-tasks/smoke-go/tests/Dockerfile:1`,
  `src/wharf/Run/RunService.cs:962`).
* Agent image preparation alone rewrites that ECR reference to
  `mars-base:latest` (`src/wharf/Image/DockerAgentImagePreparer.cs:139`,
  `src/wharf/Image/DockerAgentImagePreparer.cs:527`).
* Smoke source is copied independently from the task root into the agent
  workspace, so selecting the ECR base does not remove the local smoke fixture
  (`src/wharf/Run/RunService.cs:271`).
* Documentation still describes the local override as required
  (`docs/wharf.md:431`).

## Recommendation

Remove the agent-only local substitution and build the unmodified smoke
Dockerfile from its declared ECR image. Update the stale host behavior contract,
run targeted tests, then run the real smoke before deleting the
`mars-base:latest` alias.

## Out of scope

* Changing benchmark task ECR images
* Changing smoke source or verifier behavior
* Removing persistent CLI or composed agent cache images
