---
title: ECR Smoke Base Plan
description: Plan for running smoke from the public ECR Mars image
author: Wharf maintainers
ms.date: 2026-10-10
ms.topic: reference
---

## Tasks

1. [x] Remove local Mars substitution from
   `src/wharf/Image/DockerAgentImagePreparer.cs`.
   * Exit when Dockerfile-backed agent preparation uses its configured base
     reference unchanged.
2. [x] Update tests and `docs/wharf.md`.
   * Exit when the behavior contract requires registry-backed preparation and
     targeted tests cover the generated smoke task cache Dockerfile.
3. [x] Validate with targeted tests and a named-tmux smoke run.
   * Exit when tests pass and retained smoke evidence shows the public ECR image
     as the environment parent.
4. [x] Delete the local `mars-base:latest` image.
   * Exit when Docker inspection reports the local alias absent and the public
     ECR image present.
5. [x] Complete implementation and review artifacts.
   * Exit when evidence and residual risks are recorded.
