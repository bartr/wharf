---
title: Wharf knowledge pack
description: Evidence-derived lessons, study guide, and assessments from building and operating Wharf
---

## Purpose

AI assistants wrote most of Wharf's code. This pack shows how the engineering
experience behind it can still be captured, studied, and assessed.

Every lesson here is derived from retained repository evidence: the
[retrospective](../../RETRO.md), [session log](../session-log.md),
[release qualification](../release-qualification.md), release notes, and the
Research, Plan, Implement, Review artifacts in
[`.github/tracking/`](../../.github/tracking/). No lesson relies on
recollection alone. Where a fact is an operator observation rather than
retained telemetry, the pack says so.

## Contents

| File | Audience | Use |
|---|---|---|
| [lessons.md](lessons.md) | Everyone | What a practitioner should know after building and operating Wharf |
| [study-guide.md](study-guide.md) | Learners | Seven modules with reading lists and self-check questions |
| [assessment-1-foundations.md](assessment-1-foundations.md) | Learners | Knowledge check: short answer and multiple choice |
| [assessment-2-scenarios.md](assessment-2-scenarios.md) | Learners | Applied judgment: realistic operating scenarios |
| [failure-labs.md](failure-labs.md) | Learners and facilitators | Practice predicting, diagnosing, and learning from three safe failure scenarios |
| [llm-coach-prompts.md](llm-coach-prompts.md) | Learners and facilitators | Prompts for using an LLM as a questioning senior-developer coach |
| [answer-key.md](answer-key.md) | Assessors only | Answers, rationale, evidence, and scoring rubric |

Keep the answer key separate when the assessments are delivered.

## Using Wharf as a workshop example

Wharf is a reference example for workshops, not part of the offering. It
shows what disciplined AI-assisted software engineering can produce: a
non-trivial system built quickly, with every decision, failure, and
validation result available for inspection.

The best practices matter more than their exact form. Teams should adapt
the method to their own SDLC and tools:

| Keep the principle | Adapt the form |
|---|---|
| Investigate before building, and keep each phase separate | Use RPI as is, fold it into an existing SDLC, or scale it down for small changes |
| Decisions and evidence live with the work | Use `.github/tracking/`, a wiki, architecture decision records, or issue history |
| Validate the deliverable, not a description of it | Use existing tests, pipelines, and release gates |
| Record failures before cleaning up | Use a tracking file, an incident template, or a run log |
| Humans challenge AI confidence with evidence | Use code review, design review, or the [LLM coach prompts](llm-coach-prompts.md) |
| Turn experience into curriculum | Build lessons, labs, and assessments from the team's own projects |

Facilitators can use this pack as a template. A team that follows the
method on its own work produces its own evidence, and that evidence can
become its own lessons, failure labs, and assessments.

The draft [train-the-trainer plan](../ttt-facilitator-plan.md) applies this
approach to a familiar URL-shortener exercise across discovery and
specification, implementation, and operations.

## How this extends the method

The method normally produces software plus tracking evidence. This pack adds a
third output: **transferable knowledge**. The same evidence that explains how
the software was built becomes curriculum:

1. Tracking artifacts record decisions, alternatives, and validation results.
2. Retrospectives turn surprises into explicit lessons.
3. Lessons become study material that links back to the original evidence.
4. Assessments check whether a learner can apply the judgment, not just recall it.

The approach is only as good as the evidence. The
[failed-run lesson](../../RETRO.md#failed-runs-need-durable-records-before-cleanup)
shows the limit: when evidence is deleted, the knowledge has to be
reconstructed from memory.

## Validity limits

Reading and passing these assessments shows understanding of recorded
decisions. It is not equivalent to having operated a 64-core benchmark host
through a failed cold run. Pair the material with a hands-on lab, such as
running Wharf on a small task set and deliberately varying `N` and `P`.
Whether this combination develops judgment comparable to direct experience
is a hypothesis to measure, not an established result.

The [failure labs](failure-labs.md) make that caveat actionable: learners
commit to a prediction, investigate a bounded scenario, explain their
reasoning, and only then compare it with Wharf's retained evidence. The
[LLM coach prompts](llm-coach-prompts.md) keep the model in a questioning and
reviewing role rather than having it supply the plan or implementation.
