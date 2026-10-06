---
name: brd
description: 'Turn existing written notes into a Business Requirements Document. Use when capturing why an initiative is worth doing, who it serves, what business outcomes define success, and what it will cost, from stakeholder notes, meeting transcripts, an email thread, a strategy memo, or a training or offering request. Reads the source files and drafts the document with cited provenance and an explicit gap list, rather than interviewing the sponsor question by question. Use before a PRD, not instead of one.'
---

# BRD

Builds a Business Requirements Document from written source material. The input is one or more notes files. The output is a completed BRD plus a gap report naming every business decision the notes did not settle.

## Overview

A BRD answers why an initiative is worth doing, who it serves, and what outcome would count as success. It stops short of specifying a solution. That boundary is what makes it useful: it forces agreement on the outcome before anyone argues about the build.

This skill works from files rather than interviews. Stakeholders write in the form they naturally use, an email thread, a strategy memo, meeting notes, or a request for an offering. The skill reads all of it, extracts what is there, maps it onto a fixed template, and marks honestly what is missing. Questions are written into the document rather than asked in sequence.

The loop is: write notes, generate BRD, read the gap list, add to the notes, regenerate.

## When to Use

| Situation                                                        | Use this skill        |
|------------------------------------------------------------------|-----------------------|
| Notes describe something the business wants, and why              | Yes                   |
| A sponsor needs a case they can approve or reject                 | Yes                   |
| Objectives and success measures are vague or unstated             | Yes, the gaps are the point |
| An offering, program, or initiative needs framing before scoping   | Yes                   |
| The business outcome is agreed and the question is what to build   | No, write a PRD       |
| The problem itself is unvalidated with real users                  | No, run discovery first |

## Where This Sits

| Document | Answers | Owns |
|----------|---------|------|
| BRD | Why do this, for whom, and what outcome counts as success | Objectives, measures, scope, constraints, risks |
| PRD | What to build to achieve that outcome | Requirements, interfaces, acceptance criteria |
| Spec | How it is built | Architecture, data, and technical contract |

Business requirements state a need and its outcome. When a line in the BRD names a screen, an endpoint, or a technology, it has crossed into PRD territory and belongs there instead.

## Prerequisites

* At least one markdown source file. Stakeholder notes, a meeting transcript, an email thread, a strategy memo, a request for an offering, or prior research all count.
* A destination path, commonly `docs/brd.md`.
* The template at [assets/brd-template.md](assets/brd-template.md).

## Quick Start

```text
Draft a BRD from notes/*.md into docs/brd.md
```

The skill reads every source, drafts against the template, labels anything not directly stated, and collects the unresolved business decisions in section 14.

## Required Steps

### Step 1: Inventory the sources

List every input file with its date and its author or origin. Read all of them in full before writing. Note which are most recent, since recency decides conflicts later.

### Step 2: Extract claims with provenance

Pull every business objective, desired outcome, audience, constraint, cost, risk, exclusion, and open decision into a working list. Each entry records the claim and the file and heading it came from. Preserve numbers, dates, audiences, and named stakeholders exactly.

### Step 3: Classify every claim

| Label         | Meaning                                            | Handling in the BRD                      |
|---------------|----------------------------------------------------|------------------------------------------|
| `stated`      | Directly supported by a source note                | Write it plainly, cite the source        |
| `derived`     | Inferred from the notes but never said outright    | Write it and label it for confirmation   |
| `open`        | No source addresses it                             | Send it to section 14, do not invent it  |
| `conflicting` | Sources disagree                                   | Record both positions and both sources   |

Stakeholder disagreement is normal and is signal. Do not resolve it by picking the more convenient position.

### Step 4: Separate outcomes from solutions

Notes usually arrive as solutions, since people describe what they want built. For each solution statement, ask what outcome it is meant to produce and record the outcome as the objective, keeping the proposed solution as an option in section 13.

When the underlying outcome cannot be recovered from the notes, keep the solution statement and mark the missing objective `open`. Do not invent a business rationale.

### Step 5: Draft the BRD

Fill the template section by section. Every requirement in section 8 traces to an objective in section 4, and every objective has at least one measure in section 5.

An objective without a measure is a wish. When no baseline or target exists in the sources, mark it `open` and make establishing the baseline a requirement rather than fabricating numbers.

### Step 6: Report the gaps

Populate section 14 with every `open` and `conflicting` item, marking which are blocking. Populate section 16 with provenance. Then stop and report:

* Where the BRD is written.
* Count of items by label.
* Blocking open questions, listed first.
* Any objective that still has no measure, and any requirement that traces to no objective.

## The No-Interview Rule

Do not interview the sponsor to fill gaps. Do not ask one question, wait, and ask another.

Missing information becomes a specific, owned, answerable question in section 14, and drafting continues. A written gap list can be circulated to several stakeholders at once, answered in any order, and reviewed later. An interview cannot.

If the author explicitly asks to be walked through the gaps conversationally, do that.

## Fidelity Rules

* Never invent an objective, a metric, a baseline, or a cost. Unsupported means `open`.
* Keep business language out of implementation detail, and keep implementation detail out of the BRD.
* Preserve named stakeholders and their stated positions, including disagreement.
* Record the do-nothing option in section 13. It is a real option and it sets the comparison.
* Preserve exclusions. An exclusion someone stated out loud is a decision worth keeping.

## Handoff

An approved BRD is the input to a PRD. Objectives and success measures become the outcomes the product must serve, scope becomes the product boundary, and constraints carry forward unchanged. Blocking items in section 14 are resolved before a PRD depends on them.

When the notes assert a business problem with no evidence that affected users experience it, say so and recommend discovery. A BRD can record an assumed problem, provided the assumption is labeled rather than presented as fact.

## Troubleshooting

| Symptom                                          | Response                                                                             |
|--------------------------------------------------|---------------------------------------------------------------------------------------|
| Notes describe only a solution                    | Recover the outcome behind it, keep the solution as an option, mark a missing objective `open` |
| No measurable success criteria anywhere           | Draft objectives, mark every measure `open`, and flag that the initiative cannot be evaluated yet |
| Stakeholders disagree on the goal                 | Record both positions as `conflicting` and mark it blocking                            |
| Objectives read like feature requests             | Rewrite as outcomes, and move the feature language to the PRD                          |
| Everything is a "must" priority                   | Flag it, since an unprioritized list gives the sponsor no decision to make             |
| Scope keeps expanding across regenerations        | Notes are accumulating rather than deciding. Close open questions in the notes first    |

> Brought to you by bartr/skills
