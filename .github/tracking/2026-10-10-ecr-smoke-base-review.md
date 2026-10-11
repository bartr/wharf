---
title: ECR Smoke Base Review
description: Final review of the ECR-backed smoke base change
author: Wharf maintainers
ms.date: 2026-10-10
ms.topic: reference
---

## Verdict

PASS

No blocking issues were found. The smoke task now uses the exact public ECR base
declared in both of its Dockerfiles, and the local Mars alias is no longer
required.

## Evidence

* The generated agent task-cache Dockerfile begins with
  `FROM public.ecr.aws/x8v8d7g8/mars-base:latest`.
* The cold smoke log recorded the ECR base as pulled before task-environment
  construction.
* The retained trial result reports `status: passed`.
* Docker inspection reports the local alias absent and the public ECR tag
  present.
* Focused tests and patch whitespace validation passed.

## Residual considerations

The public tag is mutable. Cache identity includes the inspected image ID, so a
changed ECR tag produces a new task-environment cache rather than silently
reusing an environment derived from an older image.
