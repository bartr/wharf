---
title: Wharf failure labs
description: Bounded exercises for practicing diagnosis and engineering judgment from Wharf evidence
---

# Failure labs

These exercises are designed to make the learner reason before seeing the
documented lesson. They do not require changing Wharf or risking a real run.
Use them as tabletop scenarios first. If you choose a hands-on experiment,
use a disposable environment, a small task set, explicit resource limits, and
an agreed cleanup plan. Never inject failures into a shared or production
benchmark.

## Lab method

For each lab, the learner writes down:

1. **Prediction:** What do you expect to happen, and why?
2. **Evidence needed:** What would you inspect before changing anything?
3. **Diagnosis:** Which explanation best fits the evidence? What alternatives remain?
4. **Next action:** What is the smallest safe test or correction?
5. **Debrief:** What did the actual Wharf record show, and what would you record next time?

Do not read the debrief until the learner has committed to a diagnosis and
next action. The [LLM coach prompts](llm-coach-prompts.md) are intended to
preserve that order.

## Lab 1: The cold run stalls late

**Scenario:** A 32-core Linux host starts a run with `N=64` and `P=32` while
the image cache is cold. Preparation queues build up, and the run eventually
reports Docker write errors. A warm rerun with the same settings appears
healthy.

**Your task:** Explain why the warm result does not qualify the cold-run
configuration. Identify which measurements and retained evidence you need
before choosing a safer `P`. Describe how you would test your choice without
confounding it with a change to `N`.

**Debrief — read after answering:** Wharf separates trial concurrency (`N`)
from preparation concurrency (`P`) because Docker preparation can overwhelm
the host even when trial concurrency is otherwise useful. Cold runs are the
right sizing case; warming the cache hides the pressure that matters. Change
one variable at a time, compare gate occupancy, queue depth, wait time, and
Docker errors, and preserve the failed-run record before cleanup. The
operator's reported settings for one host are not universal sizing rules.

**Evidence:** [Preparation concurrency is the stability control](../../RETRO.md#preparation-concurrency-is-the-stability-control),
[shared preparation gate research](../../.github/tracking/2026-10-08-shared-preparation-gate-research.md),
and [failed-run lesson](../../RETRO.md#failed-runs-need-durable-records-before-cleanup).

## Lab 2: The verifier times out

**Scenario:** The agent completed and produced a patch. Verification times
out. Someone proposes rerunning the whole trial because that is the easiest
way to get a green result.

**Your task:** Decide what should be retried, what evidence and cost must stay
fixed, and what should happen if the verifier fails again. Explain why the
original patch and a fresh verifier workspace matter.

**Debrief — read after answering:** A verifier timeout retries the verifier
boundary, not the agent. Preserve the exact patch and agent cost, archive the
failed verifier evidence, and use a fresh pristine verifier workspace. A
whole-trial rerun would spend again and change the measured agent outcome.
An ordinary reward-zero verification result is not retryable.

**Evidence:** [Verifier-only retry research](../../.github/tracking/2026-10-08-verifier-only-retry-research.md)
and [Session 23 in the session log](../session-log.md).

## Lab 3: The release passes checks but will not run

**Scenario:** A package passes checksum, SBOM, and metadata checks. A user
extracts it and the application fails immediately because a required runtime
file is missing.

**Your task:** Identify the gap in the qualification evidence. Define a
release check that tests the artifact a customer receives, and say what
should be retained if that check fails.

**Debrief — read after answering:** Metadata describes a package; it does not
prove the package can execute. Qualify the extracted deliverable by running
it in the intended environment. Keep the failed artifact and output as
evidence, fix the packaging issue, then rerun the release checks against the
new artifact.

**Evidence:** [Release 1.1.0](../../RETRO.md#release-110).

## Lab 4: The AI is confident, but the design feels risky

**Scenario:** An LLM recommends sharing a host directory with concurrent
containers and confidently argues that it is the simplest approach. You are
not convinced. Many upstream task images run as root, and the workload
creates and removes files concurrently. A teammate argues that a standard
Docker pattern should be safe.

**Your task:** Without deciding by authority or intuition alone, identify
the concrete risks you would test. Propose a bounded comparison that can
separate file-ownership problems from caching effects. State what evidence
you would record and what would make you change your mind.

**Debrief — read after answering:** In Wharf, the shared-volume workflow
encountered repeated permission problems with many root-running task
images. The operator reports that these disappeared after moving to
Docker-managed images and explicit artifact transfer with `docker cp`, and
that caching improved substantially. This is an operator observation, not
retained telemetry: the evidence does not isolate which change improved
caching, and the operator does not know exactly what prompted the original
hunch. A useful test would inspect ownership and cleanup behavior separately
from cache reuse, hold the workload constant, use disposable runs, and retain
the results. The transferable skill is turning an unexplained concern into a
testable risk—not memorizing that one Docker pattern is always wrong.

**Evidence:** [Operator observation: challenge confident infrastructure
advice](../../RETRO.md#operator-observation-challenge-confident-infrastructure-advice)
and [Release 0.24.0](../../RETRO.md#release-0240).

## Facilitator notes

Ask learners to state uncertainty explicitly. Credit a well-supported
alternative diagnosis even when it differs from the debrief, provided the
learner identifies what evidence would distinguish the possibilities.
Correctness is not just naming the eventual fix: assess whether the learner
preserves measurement identity, chooses a bounded test, retains evidence,
and does not substitute either AI confidence or unexplained intuition for
evidence.

These labs rehearse judgment against documented cases. Passing them is not
proof that simulated experience is equivalent to years of operating systems
under pressure; test transfer with unfamiliar scenarios as described in the
[validity limits](README.md#validity-limits).
