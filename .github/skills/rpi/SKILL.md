---
name: rpi
description: 'Run the Research, Plan, Implement, Review loop so an AI assistant verifies before it builds. Use when a change is non-trivial, spans multiple files, touches an unfamiliar codebase, or needs evidence before code. Use also when an assistant has written code against APIs that do not exist, invented patterns that do not match the codebase, or started implementing before the approach was settled. Each phase runs under a constraint that prevents the next one and ends in a durable artifact.'
---

# RPI

Research, Plan, Implement, Review. A four-phase loop that separates investigating from building, so an AI assistant produces verified work instead of plausible-looking work.

## Overview

AI assistants cannot tell investigating from implementing. Asked for code, they write code, including code for APIs that do not exist and patterns that do not match the codebase. The assistant is not lying, it is pattern-completing, and code is the pattern it was asked for.

RPI fixes this by running each phase under a constraint that prevents the next one. When the assistant knows it cannot implement, it stops optimizing for plausible code and starts optimizing for verified truth. The constraint changes the goal.

Each phase ends in a written artifact. Findings live in files, not in chat history, which is what lets a phase start from a clean context and still know everything it needs.

This skill is self-contained. It runs on its own for a single task, and it composes with a session loop or an upstream requirements document when those exist.

## When to Use

| Situation                                                     | Use RPI                        |
|---------------------------------------------------------------|--------------------------------|
| Multi-file change, or an unfamiliar area of the codebase       | Yes                            |
| The right approach is not yet settled                          | Yes, start at Research         |
| An assistant produced code against APIs that do not exist      | Yes, Research was skipped      |
| Requirements exist and need to become an ordered plan          | Yes, start at Plan             |
| Typo fix, log line, or refactor under about 50 lines           | No, the overhead exceeds the work |
| Open-ended exploration with no intended outcome                | No, run a spike                |

## Prerequisites

* A tracking directory for artifacts, conventionally `.copilot-tracking/` at the repository root.
* The ability to start a new chat or clear context between phases.
* For the Review phase, a working lint, build, and test command.

## The Four Phases

| Phase     | Constraint                                                                                                        | Artifact                                           |
|-----------|-------------------------------------------------------------------------------------------------------------------|----------------------------------------------------|
| Research  | Cannot plan or write code. Investigates, cites findings with file paths and line numbers, recommends one approach.  | `.copilot-tracking/YYYY-MM-DD-<topic>-research.md` |
| Plan      | Cannot research or write code. Turns research into an ordered, checkbox task list with exit criteria.               | `.copilot-tracking/YYYY-MM-DD-<topic>-plan.md`     |
| Implement | Cannot replan. Executes the plan task by task, verifying after each.                                                | Working code and `...-changes.md`                  |
| Review    | Cannot modify code. Validates against research and plan, runs lint, build, and test, lists follow-ups.               | `.copilot-tracking/YYYY-MM-DD-<topic>-review.md`   |

The constraints are the mechanism. A phase that quietly does the next phase's work has defeated the point, so hold the boundary even when crossing it feels efficient.

## The Context Reset Rule

Start a new chat, or clear context, between every phase.

Open the previous phase's artifact in the editor before starting the next one. A clean context per phase is what makes the constraints stick, and it prevents a long transcript from carrying an early wrong assumption through the whole loop.

## Required Steps

### Step 1: Research

Run only when the available evidence is not adequate. Reuse supplied or completed research when it is, and record why it was reused rather than repeating it.

Research is read-only. It searches the codebase and relevant external sources, separates evidence from assumption, evaluates alternatives, and recommends one approach. Every finding cites a file path and line number, or an external source.

Exit criteria: requirements, dependencies, risks, and one recommended approach are written down, along with an explicit list of what is not being taken.

### Step 2: Plan

Turn the research into an ordered, checkable task list. Planning changes no source files.

Each task carries file targets, exit criteria, and references precise enough to act on. Record deferred decisions in a parking lot rather than solving them here.

Exit criteria: a numbered task list where someone else could execute each task and tell when it is done.

### Step 3: Implement

Execute the plan task by task. Verify after each task rather than at the end.

Record material changes and truthful validation results in the changes artifact. Check a task off only when evidence exists that it is done.

When implementation needs a significant departure from the plan, record the discovery, update the affected plan tasks, and pause only the dependent work. Do not silently drift.

### Step 4: Review

Compare the implementation against the research and the plan. Review modifies no code.

Run lint, build, and test. Report execution status separately from outcome, since completed execution is not the same as accepted work. List follow-ups with a destination: a defect returns to Implement, a decision gap to Plan, an evidence gap to Research, and residual work to a new task.

## Phase Prompts

RPI is a convention, not a tool. No specific extension is required. A short prompt at the start of each phase enforces the constraint:

```text
Research:  "Research only. Investigate X, cite findings with file paths and line
            numbers, recommend one approach. Do not plan or implement. Save to
            .copilot-tracking/<date>-<topic>-research.md."

Plan:      "Plan only. Read the research file, produce an ordered checkbox task
            list with file and line references and exit criteria. Do not implement."

Implement: "Implement only. Execute the plan task by task, verify after each,
            record changes in .copilot-tracking/<date>-<topic>-changes.md."

Review:    "Review only. Validate the implementation against the research and plan.
            Run lint, build, and test. Identify follow-ups. Do not modify code."
```

## Entry Points

Start at the phase that owns the next real action. Running all four when the first two are already satisfied is ceremony.

| Available evidence                          | Start at  |
|---------------------------------------------|-----------|
| Nothing settled, approach unknown           | Research  |
| Requirements or a PRD exist and are adequate | Plan      |
| An approved plan exists                     | Implement |
| Implementation evidence is ready            | Review    |

## Going Backward

The loop is not one-way. When Research invalidates the requirements, when Implement discovers the plan is wrong, or when Review finds a gap in evidence, return to the owning phase and record why. Returning is the loop working, not a failure. What is not acceptable is continuing forward on a premise already known to be wrong.

## Composition

RPI runs standalone. It also fits inside other workflows:

* Inside a session loop, one session is about one RPI cycle. The session supplies the frame before Research and the close ritual after Review. A fit check sits between Plan and Implement, where the plan is compared against the time available.
* Downstream of a requirements document, the PRD supplies scope, interface contract, and acceptance criteria. Its open questions become Research targets, and its acceptance criteria become Review's checklist.
* Needing more than one cycle usually means the work is more than one task.

## Troubleshooting

| Symptom                                        | Probable cause                              | Fix                                                     |
|------------------------------------------------|---------------------------------------------|---------------------------------------------------------|
| Code references APIs that do not exist         | Research was skipped or rushed              | Require a research artifact before planning             |
| Plan drifts during implementation              | Plan lacked specifics or exit criteria      | Sharpen tasks, and cut scope before starting            |
| A phase does the next phase's work             | The constraint was not stated in the prompt | Restate the constraint and restart the phase            |
| Later phases repeat earlier investigation      | Context was carried in chat, not artifacts  | Reset context and open the prior artifact instead       |
| Review passes but the work is wrong            | Review checked code, not requirements       | Validate against research and plan, not just tests      |
| The loop feels like overhead                   | The task is trivial                         | Skip RPI and just make the change                       |

## Credits

The RPI workflow is from Microsoft [HVE Core](https://github.com/microsoft/hve-core), MIT licensed. Credit Microsoft when referencing RPI.

> Brought to you by bartr/skills
