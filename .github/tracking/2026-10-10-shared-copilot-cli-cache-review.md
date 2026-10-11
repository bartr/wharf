---
title: Shared Copilot CLI Cache Review
description: Final RPI review of the shared Copilot CLI image cache
author: Wharf maintainers
ms.date: 2026-10-10
ms.topic: reference
---

## Verdict

PASS

The implementation matches the approved plan and preserves the authoritative
task environment while eliminating repeated per-container Copilot npm
installation. No blocking functional, reliability, security, or regression
issues were found.

## Findings

No blocking findings.

## Review evidence

* The exact adapter-verified effective CLI version enters image preparation.
* ECR-backed tasks retain their configured image as the environment parent.
* Dockerfile-backed tasks receive deterministic persistent environment caches.
* CLI payloads are isolated in versioned and platform-specific images.
* Task and CLI composition uses BuildKit `COPY --link`.
* Composed cache identity includes immutable environment and CLI image IDs.
* Only the unique trial child is registered for run cleanup.
* Workspace preparation validates the cached package version and does not run
  npm.
* The execution path resolves Copilot from `/opt/wharf/copilot`.
* Cache evidence contains image identities and outcomes without credential
  values.
* Cache image builds verify that the expected output image exists.
* Cache images carry explicit Wharf ownership, kind, and schema labels.
* Release build, format verification, targeted tests, full tests, cold smoke,
  warm smoke, and final labeled-cache smoke passed.
* `git diff --check` passed.

## Residual considerations

* Cache pruning is intentionally deferred. Persistent images consume local
  Docker storage until an operator removes them or a future pruning command is
  added.
* Verifier image caching remains outside this change.
* A missing or externally altered cache image fails explicitly during build or
  workspace validation; automatic cache repair is not part of this version.
