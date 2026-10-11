---
title: Resume Command Plan
description: Ordered implementation plan for exact logical-attempt resume
author: Wharf maintainers
ms.date: 2026-10-11
ms.topic: reference
---

## Tasks

1. [ ] Add a retained-run resume planner.
   * Parse arm identity and initial plan metadata.
   * Match every expected task/checksum/attempt directory.
   * Select only directories without terminal `result.json`.
   * Preserve existing retry counts and known cost.
2. [ ] Extend `RunService` with an explicit existing-run execution target.
   * Reuse the original run directory and logical attempt numbers.
   * Archive partial root evidence under `resume-history`.
   * Continue retry numbering without rerunning completed trials.
3. [ ] Replace the placeholder with `wharf resume`.
   * Support `--run-directory`, `--tasks`, `--dry-run`, `--n`, `--p`, and
     verbosity.
   * Use the normal credential environment contract.
4. [ ] Add focused planner, archive, and CLI tests.
   * Verify exact missing selection, malformed evidence rejection, retry
     continuation, dry-run output, and completed-result preservation.
5. [ ] Update operator documentation and record validation.
6. [ ] Review against this plan without modifying source.
