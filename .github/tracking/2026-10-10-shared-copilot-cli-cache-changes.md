---
title: Shared Copilot CLI Cache Changes
description: Implementation and validation evidence for linked persistent Copilot CLI images
author: Wharf maintainers
ms.date: 2026-10-10
ms.topic: reference
---

## Changes

* Extended agent image preparation with the adapter-verified effective Copilot
  version.
* Retained configured ECR task images as authoritative environment parents.
* Added deterministic persistent task-environment caches for Dockerfile-backed
  tasks.
* Added one versioned and platform-specific Copilot payload image containing
  `/opt/wharf/copilot`.
* Composed task and CLI images with BuildKit `COPY --link`.
* Added deterministic composed agent cache images keyed by environment and CLI
  image identities.
* Kept unique logical-trial children ephemeral and excluded persistent cache
  images from `DockerRunCleanup`.
* Replaced workspace npm installation with an exact package-version check.
* Added structured `agent-image-cache.json` evidence.
* Updated agent image and workspace unit tests and the agent image lifecycle
  documentation.

## Validation

* `dotnet build wharf.slnx --configuration Release --no-restore`: PASS
* `dotnet format wharf.slnx --verify-no-changes --no-restore`: PASS
* Targeted image and agent execution tests: PASS, 23 of 23
* Full `wharf.Tests` suite: PASS, 202 of 202
* Cold smoke after CLI payload removal: PASS
  * Copilot payload built in 30 seconds
  * Composed agent cache built in 6 seconds
  * Trial passed with `f2p=100%` and `p2p=100%`
* Warm smoke: PASS
  * Task environment cache hit
  * Copilot CLI cache hit
  * Composed agent image cache hit
  * Image preparation reached workspace preparation in 2 seconds
  * Trial passed with `f2p=100%` and `p2p=100%`
* Final labeled-cache smoke: PASS
  * Task environment rebuilt with cache ownership labels
  * Copilot CLI cache reused
  * Composed agent cache rebuilt from the labeled environment
  * Trial passed with `f2p=100%` and `p2p=100%`

## Retained evidence

Smoke artifacts are retained outside the repository under the session workspace:

```text
files/cli-cache-smoke/
```

The final smoke cache evidence records CLI version `1.0.95`, a CLI cache hit,
and the composed agent cache identity.
