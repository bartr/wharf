---
title: Wharf study guide
description: Seven modules for learning the engineering judgment recorded in Wharf's evidence
---

## How to use this guide

Work through the modules in order. For each, read the listed evidence, then
answer the self-check questions in your own words before checking the source.
Read [lessons.md](lessons.md) first for an overview.

Estimated time: four to six hours of reading, plus the optional failure labs.

## Module 1: The workflow and its evidence

**Goal:** Understand how Sessions and RPI produced trustworthy AI-assisted work.

**Read:**

* [Development approach](../../RETRO.md#development-approach)
* [Benefits of Sessions and RPI](../../RETRO.md#benefits-of-sessions-and-rpi)
* One complete RPI set, such as the shared preparation gate
  [research](../../.github/tracking/2026-10-08-shared-preparation-gate-research.md),
  [plan](../../.github/tracking/2026-10-08-shared-preparation-gate-plan.md),
  [changes](../../.github/tracking/2026-10-08-shared-preparation-gate-changes.md),
  and [review](../../.github/tracking/2026-10-08-shared-preparation-gate-review.md)

**Self-check:**

1. What constraint does each RPI phase impose, and why does it matter?
2. Where does the research artifact separate evidence from assumption?
3. Why is "execution status" reported separately from "outcome" in a review?

## Module 2: Scaling up on one host

**Goal:** Explain why `N` and `P` are independent, and how to choose them.

**Read:**

* [Preparation concurrency is the stability control](../../RETRO.md#preparation-concurrency-is-the-stability-control)
* [Container execution and qualification hardening](../../RETRO.md#container-execution-and-qualification-hardening-0210-to-0230)
* [N=32 qualification checkpoint and Sessions 22 and 23](../session-log.md)

**Self-check:**

1. Which host resources does `N` mostly consume, and which does `P` protect?
2. Why is the cold run the right case for sizing `P`?
3. What does the gate telemetry show about the cost of a conservative `P`?

## Module 3: Failure, retry, and recovery

**Goal:** Choose the correct retry boundary and preserve measurement identity.

**Read:**

* [Verifier-only retry research](../../.github/tracking/2026-10-08-verifier-only-retry-research.md)
* [Resume progress reconciliation research](../../.github/tracking/2026-10-11-resume-progress-reconciliation-research.md)
* Session 7, 12, and 13 entries in [Session retrospectives](../../RETRO.md#session-retrospectives)

**Self-check:**

1. Which failures are terminal, which retry the verifier, and which retry the whole trial?
2. Why must a shared build outlive one caller's cancellation?
3. Why is recovery planned as a pure function over validated events?

## Module 4: Isolation, identity, and safety

**Goal:** Keep agent and verifier environments separate and correctly identified.

**Read:**

* [Release 0.24.0](../../RETRO.md#release-0240)
* [Container version identity must not depend on the host CLI](../../RETRO.md#container-version-identity-must-not-depend-on-the-host-cli)
* Session 8 and 9 entries in [Session retrospectives](../../RETRO.md#session-retrospectives)

**Self-check:**

1. Why did Wharf replace host bind mounts with explicit container lifecycles?
2. Why must each ECR-backed trial build its own child image?
3. How does the adapter keep credential values out of arguments and logs?

## Module 5: Measurement you can defend

**Goal:** Compute and report benchmark results that survive scrutiny.

**Read:**

* [Benchmark evidence expanded without merging benchmark universes](../../RETRO.md#benchmark-evidence-expanded-without-merging-benchmark-universes)
* [Enterprise benchmark](../../RETRO.md#enterprise-benchmark-net-coverage-at-a-fraction-of-the-cost)
* [Enterprise summary](../../enterprise/data/summary.md)

**Self-check:**

1. Why does PASS@1 use the planned denominator?
2. Why are missing costs never imputed?
3. Why are DeepSWE and enterprise pass rates not interchangeable?

## Module 6: Qualification and release discipline

**Goal:** Know what evidence a release claim needs.

**Read:**

* [Release 1.0.0](../../RETRO.md#release-100)
* [Release 1.1.0](../../RETRO.md#release-110)
* [Release qualification](../release-qualification.md)
* [Failed runs need durable records before cleanup](../../RETRO.md#failed-runs-need-durable-records-before-cleanup)

**Self-check:**

1. Why can a package pass every metadata check and still fail?
2. What did the 1.0.0 review find that the passing test suite did not?
3. What should be recorded before deleting a failed job directory?

## Module 7: Learning from failure with an LLM coach

**Goal:** Practice engineering judgment by predicting, diagnosing, and
explaining a failure before reading its documented resolution.

**Read:**

* [Failure labs](failure-labs.md)
* [LLM coach prompts](llm-coach-prompts.md)
* [Failed runs need durable records before cleanup](../../RETRO.md#failed-runs-need-durable-records-before-cleanup)

**Self-check:**

1. What must you commit to before asking the coach for help?
2. How do you tell a useful coaching question from an answer that short-circuits learning?
3. What evidence would change your diagnosis, and what is still only a hypothesis?

**Practice:** Complete one failure lab without reading its debrief first.
Use the coach to examine your reasoning, not to write code or choose for you.

## Optional hands-on lab

On a Linux host with Docker, run Wharf on a small task set with a cold image
cache. Record a run once with a low `P` and once with a high `P` at the same
`N`. Compare preparation-gate wait times and any Docker errors, then write a
tracking entry for each run before deleting the job directories.

For a safer first exercise, complete the [failure labs](failure-labs.md) as
tabletop scenarios. Only run experiments in a disposable environment with
explicit resource limits; do not inject failures into a shared or production
benchmark run.
