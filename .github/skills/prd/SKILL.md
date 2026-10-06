---
name: prd
description: 'Review written notes for PRD readiness, propose explicit assumptions for missing decisions, and generate a human PRD plus an LLM-oriented JSONL requirements ledger when the sources are ready. Use for stream-of-consciousness notes, brain dumps, transcripts, design scratch files, idea docs, research output, and PRD regeneration after sources change.'
---

# PRD

Reviews written source material before generating a Product Requirements
Document. The input is one or more stream-of-consciousness markdown files. The
workflow first produces a readiness review containing gaps, conflicts, and
proposed assumptions. Once the sources are ready, it produces both a human PRD
and a lossless JSONL requirements ledger for LLM consumption.

## Overview

Most PRD tooling interrogates the author: a question, an answer, another question, repeated until a document exists. That works, but it is slow, it loses fidelity when the author is describing something they already wrote down, and it produces answers shaped by the question order rather than by the thinking.

This skill inverts that. The author writes freely, in whatever structure they think in, across as many files as they like. The skill reads all of it, extracts what is actually there, maps it onto a fixed template, and marks honestly what is missing. Questions still get asked, but in writing, at the end, batched into the document itself.

The loop is: write notes, review readiness, update the notes, review again, then
generate the PRD and JSONL ledger. The notes stay the source of truth. Proposed
assumptions are never silently promoted into requirements.

## When to Use

| Situation                                                        | Use this skill        |
|------------------------------------------------------------------|-----------------------|
| Notes, transcripts, or a brain dump need a readiness review        | Yes                    |
| Notes are ready and need a human PRD plus an LLM artifact           | Yes                    |
| A PRD needs updating after new notes were written                   | Yes, review then regenerate |
| Discovery output needs to become buildable requirements             | Yes                    |
| Nothing is written down and the problem is unvalidated             | No, run discovery first |
| Requirements are settled and the next step is building             | No, go to research      |

## Prerequisites

* At least one markdown source file. Anything counts: freeform notes, a meeting transcript, an architecture ramble, a chat export, a bulleted idea list, or prior research output.
* A destination path for the readiness review, commonly
  `docs/<product>-prd-readiness.md`.
* When generation is requested, destination paths for the PRD and JSONL
  ledger, commonly `docs/<product>-prd.md` and
  `docs/<product>-requirements.jsonl`.
* The template at [assets/prd-template.md](assets/prd-template.md).
* The templates at [assets/readiness-template.md](assets/readiness-template.md)
  and [assets/requirements-jsonl.md](assets/requirements-jsonl.md).

More sources produce a better draft. Fewer sources produce a shorter draft with a longer gap list, which is the correct behavior rather than a failure.

## Quick Start

Point the skill at the notes and ask for a readiness review.

```text
Review notes/*.md for PRD readiness and write docs/product-prd-readiness.md
```

After updating the source notes until no blocking gaps remain:

```text
Generate docs/product-prd.md and docs/product-requirements.jsonl
```

Do not generate either final artifact merely because a readiness review was
requested.

## What Counts as a Source

Sources are not expected to be organized, complete, or consistent. Handle each of these:

| Source type              | Typical contribution                              |
|--------------------------|---------------------------------------------------|
| Stream-of-consciousness  | Intent, constraints, and strong opinions          |
| Meeting notes            | Decisions, disagreements, and owners              |
| Transcripts              | Rationale and rejected alternatives               |
| Architecture scratch     | Components, data shape, and interfaces            |
| Research output          | Evidence, benchmarks, and prior art               |
| Existing PRD or spec     | The baseline being revised                        |

## Operating Modes

### Readiness mode

Use readiness mode by default when the user asks to inspect, review, check, or
prepare notes before generating a PRD. Produce only the readiness review.

### Generation mode

Use generation mode only when the user explicitly asks to generate or
regenerate the PRD. Re-run readiness first. If blocking gaps or conflicts
remain, update the readiness review and stop without generating final
artifacts, unless the user explicitly accepts named unresolved items.

Generation mode always emits both:

* a human-readable PRD; and
* an LLM-oriented JSONL requirements ledger containing the same claims without
  prose compression.

## Required Steps

### Step 1: Inventory the sources

List every input file with its date. Read all of them in full before writing anything. Order matters later for conflicts, so record which notes are most recent.

### Step 2: Extract claims with provenance

Pull every candidate requirement, constraint, decision, exclusion, and open question into a working list. Each entry records the claim and the file and heading it came from. Do not paraphrase away specifics such as numbers, formats, names, and bounds.

### Step 3: Classify every claim

| Label        | Meaning                                                        | Handling in the PRD                       |
|--------------|----------------------------------------------------------------|-------------------------------------------|
| `stated`     | Directly supported by a source note                            | Write it plainly, cite the source         |
| `derived`    | Inferred from the notes but never said outright                | Write it and label it for confirmation    |
| `open`       | No source addresses it                                         | Send it to section 15, do not invent it   |
| `deliberate` | Intentionally left to the implementer                          | Record it in section 1.1 with the reason  |

The distinction between `open` and `deliberate` is the important one. An `open`
item is a gap in the thinking. A `deliberate` item is a decision to leave room.
Never quietly convert one into the other.

### Step 4: Build the readiness review

Map the source claims onto every PRD section and write the readiness review
using `assets/readiness-template.md`.

For every missing decision, write:

* a specific gap;
* whether it blocks PRD generation or implementation;
* the consequence of leaving it unresolved;
* one proposed assumption that would close it; and
* the source heading where the author should record the decision.

A proposed assumption is a review aid, not a requirement. Give it a stable ID
such as `A-001` and one of these statuses:

| Status | Meaning |
|--------|---------|
| `proposed` | Suggested default with no source support yet |
| `confirmed` | Explicitly supported by an updated source |
| `rejected` | Source chose a different answer |
| `deliberate` | Source explicitly delegates the choice |

Never write a proposed assumption into a source file unless the user explicitly
asks. The normal loop is for the author to update the raw notes in their own
words, after which the next review reclassifies the claim as `stated`.

### Step 5: Resolve conflicts

When sources disagree, do not silently pick one. Prefer the most recent note, then record the conflict as an open question naming both positions and both sources. Contradictions that affect the interface contract or acceptance criteria are blocking.

### Step 6: Apply the readiness gate

The sources are ready for final generation only when:

* no blocking source conflict remains;
* every public interface, persisted data contract, security boundary, and
  acceptance criterion is stated or deliberately delegated;
* goals and non-goals define a coherent release boundary;
* every numerical target has a measurement method;
* every proposed assumption is confirmed, rejected, or deliberate; and
* the remaining open items are explicitly non-blocking.

Report `READY` or `NOT READY` at the top of the readiness review. A large volume
of notes is not evidence of readiness.

### Step 7: Draft the PRD

Fill the template section by section. Keep every heading. When a section has no material, write "Not applicable" with a reason, or leave it `open`, so the document's shape stays comparable to other PRDs.

Write acceptance criteria in section 14 that are checkable without interpretation. Anything not checkable is not a requirement, so either sharpen it or move it to goals.

### Step 8: Generate the LLM requirements ledger

Generate a JSONL file using `assets/requirements-jsonl.md`. This file is a
lossless, atomic representation of the same source claims used for the PRD.

Requirements for the ledger:

* one JSON object per line;
* UTF-8 with no comments, Markdown fences, or multi-line JSON records;
* a metadata record first;
* stable IDs for requirements, decisions, constraints, non-goals, acceptance
  criteria, deliberate choices, and open questions;
* exact source path and heading provenance on every claim;
* explicit relationships among goals, requirements, and acceptance criteria;
* exact numbers, defaults, enum values, commands, and bounds;
* no unsupported assumptions;
* no narrative shortening that drops conditions or exceptions; and
* deterministic ordering by record type and ID.

JSONL is preferred over a single JSON document because claims can be streamed,
diffed, selected, embedded, or supplied to an LLM independently. Do not use
JSONL for source notes that contain only a few inseparable narrative decisions;
in that case use one structured Markdown handoff. Record the format decision in
the readiness review.

Validate that every line parses as JSON, every referenced ID exists, IDs are
unique, and every stated claim has provenance.

### Step 9: Report the result

Populate section 15 with every `open` item, marking which are blocking. Populate section 16 with the provenance table. Then stop and report:

* Where the readiness review, PRD, and JSONL ledger are written.
* Count of items by label.
* The blocking open questions, listed first.
* Any conflicts found across sources.
* The readiness verdict.
* The number of JSONL records by type.

## The No-Interview Rule

Do not interview the author to fill gaps. Do not ask a question, wait, and ask another.

When information is missing, write it into the readiness review as a specific
answerable gap with a proposed assumption. Do not generate the final PRD while
blocking gaps remain. A written gap list is reviewable, durable, and answerable
in the author's own order. An interview is none of those things.

The author closes gaps by adding to the notes and regenerating. If the author explicitly asks to be walked through the gaps conversationally, do that, since the rule protects their attention rather than restricting them.

## Fidelity Rules

* Never invent a requirement. An unsupported statement is `open`, not a guess.
* Never present a proposed assumption as settled. It remains `proposed` until a
  source confirms it.
* Preserve specifics exactly. Numbers, formats, identifiers, and bounds carry meaning.
* Preserve rejected alternatives and the reasons, since they prevent the decision being relitigated.
* Keep the author's terminology. Renaming their concepts makes the PRD harder for them to verify.
* Do not smooth over disagreement between sources into agreeable prose.
* Deciding what to leave unspecified is part of the work, not a shortcut. Justify each entry in section 1.1.

## Updating an Existing PRD

Regenerating is the normal path. When a PRD already exists:

1. Treat the current PRD as one more source, and the most authoritative for decisions already made.
2. Apply new notes on top of it.
3. Preserve section 1.1 entries unless a note explicitly closes one.
4. Report what changed: newly closed questions, new opens, and any decision a note reversed.
5. Re-run the readiness gate.
6. Regenerate both the human PRD and JSONL ledger from the same claim list.
7. Bump the status and date in the header.

## Handoff

The finished PRD is the human input to implementation. The JSONL ledger is the
machine input for planning, traceability, retrieval, validation, and future PRD
regeneration. Both are projections of the same source-backed claim list;
neither is authoritative over the raw notes.

When the problem itself is unvalidated rather than merely unwritten, the notes will show it: no users, no evidence, and a solution asserted from the start. Say so plainly and recommend discovery before treating this document as requirements.

## Troubleshooting

| Symptom                                     | Response                                                                             |
|---------------------------------------------|--------------------------------------------------------------------------------------|
| Readiness review is mostly `open`           | Correct outcome for thin notes. Deliver it so the author knows what to add |
| Sources contradict each other               | Prefer the most recent, record both positions as a blocking question                   |
| Notes describe a solution with no problem    | Draft what is there and flag that discovery, not a PRD, is the missing step            |
| User asks for a PRD with blocking gaps       | Refresh the readiness review and stop unless the user explicitly accepts each gap       |
| Human PRD and JSONL disagree                 | Regenerate both from the same claim list; never patch one independently                  |
| Section 1.1 is empty                        | Ask what should be left to the implementer, or state plainly that everything is fixed  |
| Acceptance criteria are vague               | Rewrite as checkable statements, or demote them to goals                               |
| The PRD keeps growing on regeneration       | Notes are accumulating rather than deciding. Close open questions in the notes first    |

## Credits

The template structure, and the deliberate underspecification discipline in section 1.1, are derived from the specification in [context-first/movies-template](https://github.com/context-first/movies-template), MIT licensed.

> Brought to you by bartr/skills
