---
name: design-thinking
description: 'Validate the real customer problem before anything gets built. Use when a request arrives as a proposed solution, when the users, outcome, or requirements are still unclear, or when preparing requirements for a rapid prototype, discovery session, or customer workshop. Produces a framed problem, a tested low-fidelity concept, and an evidence-backed PRD handoff for the RPI workflow. Do not use when requirements are already validated and approved.'
---

# Design Thinking

A focused discovery workflow that turns an ambiguous request or a proposed solution into a validated problem statement and an evidence-backed PRD that the RPI workflow can act on.

## Overview

Most failed delivery work is not badly built. It is built against an assumed problem. A customer arrives with a solution already in mind, the team treats that solution as a requirement, and a polished prototype answers the wrong question.

This skill inserts a short, evidence-driven discovery pass before implementation. It covers six activities and ends with a PRD handoff. It is intentionally lighter than a full nine-method Design Thinking framework so it fits inside a workshop, a discovery call, or a rapid prototyping session.

Run this skill when the problem is uncertain. Skip it when the problem, users, and acceptance criteria are already agreed, and go straight to the PRD and RPI research.

## When to Use

| Situation                                                             | Use this skill  |
|-----------------------------------------------------------------------|-----------------|
| Request arrives as a solution ("build us a dashboard", "use this app") | Yes             |
| Customer need or user is unclear, assumed, or second-hand              | Yes             |
| Preparing a prototype for a customer or partner event                  | Yes             |
| Multiple stakeholders disagree about the goal                          | Yes             |
| Requirements are validated, scoped, and accepted                       | No, go to PRD   |
| Bug fix, refactor, or bounded engineering task                         | No, go to RPI   |

## Prerequisites

* Access to at least one person who has the problem, or written evidence from one. Interviews, tickets, transcripts, support logs, and recorded sessions all qualify.
* A working directory for artifacts. This skill writes to `.copilot-tracking/design-thinking/{project-slug}/`.
* Agreement from the requester that the proposed solution may change based on evidence.

State clearly when no user evidence is available. Proceed with assumptions explicitly labeled rather than presenting guesses as findings.

## Quick Start

Ask the user for the request, the customer or team involved, and any existing material. Then work the six activities in order, confirming each before moving on.

```text
1. Scope conversation        -> what is being asked, and why
2. Evidence and assumptions  -> what is known versus believed
3. Problem framing           -> one testable problem statement
4. Concept exploration       -> three or more options, including theirs
5. Low-fidelity validation   -> smallest artifact that tests the concept
6. PRD handoff               -> evidence-backed requirements for RPI
```

Stop early and hand off when the evidence is already sufficient. Completing all six activities is not a goal in itself.

## Activity 1: Scope Conversation

Separate the request from the underlying need.

Capture:

* The requested solution, recorded verbatim so it is not lost or paraphrased away.
* The outcome the requester expects that solution to produce.
* The people who do this work today, and who is affected by it.
* How the work happens now, including workarounds.
* Constraints that are real, such as deadlines, budgets, platforms, regulations, and existing systems.
* Who decides, who funds, and who signs off.

Ask why the outcome matters until reaching a business or user consequence rather than a feature preference.

Write findings to `01-scope.md`.

## Activity 2: Evidence and Assumption Check

Sort everything gathered so far into confidence levels. This is the step that prevents assumptions from being promoted into requirements.

| Marker        | Meaning                                       | Required action                       |
|---------------|-----------------------------------------------|---------------------------------------|
| `validated`   | Supported by direct evidence from users or data | Use as reliable input                 |
| `assumed`     | Believed true, not yet verified                 | Plan a verification step              |
| `unknown`     | A gap that blocks a confident decision          | Investigate before committing         |
| `conflicting` | Sources disagree                                | Resolve before any downstream work    |

For every `assumed` and `unknown` item, record how it could be checked and what it would cost to be wrong. Escalate any `conflicting` item to the decision maker before framing the problem.

Write findings to `02-evidence.md`.

## Activity 3: Problem Framing

Produce one problem statement that can be argued with and tested.

```markdown
User: [specific role or segment, not "the business"]
Job: [what they are trying to accomplish]
Barrier: [what blocks them today, with evidence reference]
Impact: [measurable consequence: time, cost, error rate, risk, revenue]
Success: [observable change that means this is solved]
Out of scope: [explicitly excluded, to protect the timebox]
```

Validate the statement against these checks:

* It names a user, not a department or a technology.
* It describes a problem, not a chosen solution.
* Its impact is measurable, or an explicit unknown to be measured.
* Someone could disagree with it based on evidence.

Confirm the framing with the requester before generating concepts. This is the cheapest possible moment to be wrong.

Write findings to `03-problem.md`.

## Activity 4: Concept Exploration

Generate at least three distinct ways to address the framed problem. Always include the originally requested solution as one option so it is evaluated rather than dismissed or assumed.

For each concept record:

* How the user experiences it, described as a short scenario.
* What makes it viable or risky.
* What must be true for it to work, using the confidence markers.
* What it does not solve.

Evaluate concepts against desirability, feasibility, and viability. Select one to test and record why the alternatives were set aside. A rejected option with a documented reason is a durable decision.

Write findings to `04-concepts.md`.

## Activity 5: Low-Fidelity Validation

Build the smallest artifact that tests the riskiest assumption in the selected concept. A sketch, a whiteboard capture, a screen sequence, a sample output, or a walkthrough script are all valid.

Keep fidelity deliberately low:

* Do not polish visuals, apply branding, or refine copy.
* Do not build production code, real integrations, or persistence.
* Do not expand scope beyond the assumption being tested.

Polish creates false confidence. Reviewers respond to a finished-looking artifact by critiquing its appearance instead of challenging its premise, and stakeholders read polish as commitment.

Before testing, write down what result would prove the concept wrong. Then capture what users actually did and said, separately from interpretation.

Write findings to `05-validation.md`.

## Activity 6: PRD Handoff

Produce the PRD that carries the discovery evidence into delivery.

```markdown
# PRD: [title]

## Problem
[validated problem statement from Activity 3]

## Users and stakeholders
[who is affected, who decides]

## Evidence
[findings with confidence markers and sources]

## Decision
[selected concept, and the alternatives rejected with reasons]

## Scope
In scope: [...]
Out of scope: [...]

## Success measures
[observable, measurable outcomes]

## Acceptance criteria
[testable statements that define done]

## Open questions
[unknown and conflicting items that remain, with owners]

## Confidence
[overall readiness, and what would change this PRD]
```

Every requirement traces back to evidence or is explicitly labeled as an assumption. Unresolved `conflicting` items block handoff.

Write the PRD to `prd.md`.

## Handoff to RPI

Hand the PRD to `rpi-research`, which treats the entries as follows:

| PRD content            | RPI research behavior                          |
|------------------------|------------------------------------------------|
| `validated` findings   | Accept as evidence, do not re-investigate      |
| `assumed` findings     | Verify before planning depends on them         |
| `unknown` items        | Treat as primary research targets              |
| Acceptance criteria    | Carry into planning as checkable requirements  |
| Out of scope           | Enforce as a boundary during implementation    |

Return to this skill when research invalidates the problem statement, reveals an unrepresented user, or surfaces evidence that contradicts the selected concept. Returning is a sign the workflow is working, not a failure.

## Quality Rules

* Never present an assumption as a finding. Label confidence on every claim.
* Never let the requested solution bypass framing. Evaluate it as one option.
* Keep validation artifacts rough until the concept is confirmed.
* Record decisions and rejected alternatives so they are not relitigated.
* Timebox discovery to fit the engagement, and hand off with explicit unknowns rather than stalling for certainty.

## Troubleshooting

| Problem                                        | Response                                                                              |
|------------------------------------------------|---------------------------------------------------------------------------------------|
| No access to real users                        | Proceed with labeled assumptions and make user access the top open question           |
| Requester insists on their solution            | Keep it as a concept, frame the problem, and let the validation evidence decide        |
| Stakeholders disagree on the goal              | Record positions as `conflicting` and escalate to the decision maker before framing    |
| Scope keeps expanding                          | Return to the problem statement and enforce the out-of-scope list                      |
| Session is running out of time                 | Stop at the current activity and hand off the PRD with unresolved items marked         |
| Prototype feedback is only about look and feel | Fidelity is too high; reduce the artifact and retest the underlying assumption          |

> Brought to you by bartr/skills
