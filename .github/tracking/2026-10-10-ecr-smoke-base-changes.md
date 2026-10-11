---
title: ECR Smoke Base Changes
description: Changes and validation for removing the local Mars image dependency
author: Wharf maintainers
ms.date: 2026-10-10
ms.topic: reference
---

## Changes

* Removed the special-case rewrite from the public Mars ECR reference to
  `mars-base:latest`.
* Preserved the configured Dockerfile unchanged in task-environment evidence.
* Added a focused test for the smoke Dockerfile's configured ECR base.
* Updated the host and Docker behavior contract.
* Removed the obsolete local override Dockerfile and its setup-script build
  step. Its CLI installation is replaced by the shared versioned Copilot
  payload, and its NodeSource workaround is not needed by task-derived images.
* Removed the local `mars-base:latest` image and the stale smoke environment
  cache before validation.

## Validation

* Focused `DockerAgentImagePreparerTests`: PASS, 6 of 6
* Named-tmux smoke: PASS
  * Pulled `public.ecr.aws/x8v8d7g8/mars-base:latest`
  * Rebuilt `wharf-task-env:smoke-go-46c50b63f767`
  * Reused the Copilot CLI cache
  * Completed the agent and verifier with trial status `passed`
* Final Docker inspection:
  * `mars-base:latest` is absent
  * `public.ecr.aws/x8v8d7g8/mars-base:latest` is present
* `git diff --check`: PASS
