# [Component or Product Name] Product Requirements

Status: [Draft v0.1]
Date: [YYYY-MM-DD]
Owner: [name]
Sources: [input notes this PRD was built from]

Replace every bracketed placeholder. Keep every section heading. When a section does not apply, keep the heading and write "Not applicable" with a one-line reason, so PRDs stay comparable to each other.

Label any statement that is not directly supported by a source note:

* `derived` means inferred from the notes and awaiting confirmation.
* `open` means not addressed by any source and needs a decision.
* `deliberate` means intentionally left to the implementer, recorded in section 1.1.

## 1. Overview

[Two to four sentences. What is being built, who it is for, and why now. State the shape of the thing, not its implementation.]

### 1.1 Deliberately Unspecified

This section is intentional, not an omission. List the decisions left to the implementer and say why each one is theirs to make.

Fix only what must be fixed for the result to be correct, comparable, or contractual. Leave open anything where the reasoning is part of the work, where a defensible choice is more valuable than a prescribed one, or where prescribing would measure typing speed rather than judgment.

| Fixed here                          | Left to the implementer            |
|-------------------------------------|------------------------------------|
| [public contract, section 6]        | [data model and schema]            |
| [acceptance criteria, section 14]   | [internal structure and libraries] |
| [performance bar, section 10.4]     | [naming, layout, and ergonomics]   |

If an implementer wishes this document answered one of the open items, that is the intended behavior. They should research it, make a defensible call, record the decision, and move on.

## 2. Goals

[Bulleted, outcome-shaped, and checkable. Each goal should be traceable to an acceptance criterion in section 14.]

## 3. Non-Goals

[Explicit exclusions. This is the scope boundary that protects the timebox. Name the tempting adjacent work that is out of scope.]

## 4. Architecture

### 4.1 Components

| Component | Purpose | Notes |
|-----------|---------|-------|
| [name]    | [role]  | [constraints or supplied artifacts] |

### 4.2 Shape

[A diagram or a short description of how the components relate. Keep it at the boundary level: what talks to what, and in which direction.]

## 5. Data

### 5.1 Source and format

[Where the data comes from, what format it is in, and where it lives.]

### 5.2 Packaging

[How data is delivered to the running system.]

### 5.3 Loading

[How the system ingests data at startup or runtime.]

### 5.4 Seed data

[What representative data exists for development and testing.]

### 5.5 Schema

[Either the fixed schema, or an explicit statement that implementers determine it by inspecting the source data. If the latter, record it in section 1.1.]

## 6. Interface Surface

[The public contract. This is normally fixed, because it is what makes independent implementations comparable and what consumers depend on.]

| Method or command | Path or name | Notes |
|-------------------|--------------|-------|
| [GET]             | [/api/thing] | [parameters, error cases] |

### 6.1 Validation rules

[Rules that must produce a defined error. State bounds, formats, defaults, and what happens on violation.]

* [parameter] within [range], default [value]
* [parameter] matches [pattern]

## 7. Observability

### 7.1 Metrics

[What must be observable and in what format. Whether specific metric names, labels, and buckets are fixed or left open belongs in section 1.1.]

### 7.2 Logging

[Destination, format, levels, configuration, and what must never be logged.]

### 7.3 Dashboards

[What must be visible, provisioned how, and for whom.]

## 8. Deployment

[Target environment, packaging, health and readiness signals, resource requests and limits, and security context.]

## 9. Build and Packaging

[Build inputs and outputs, image or artifact requirements, and what automation is required versus optional.]

## 10. Testing and Benchmarks

### 10.1 Unit tests

[Scope and coverage expectations.]

### 10.2 Integration tests

[What is exercised together, and against what data.]

### 10.3 End-to-end tests

[What validates the full contract from outside the system, including negative cases for every validation rule in section 6.1. State any tooling that is explicitly out of scope.]

### 10.4 Performance targets

[Measurable bars, such as latency percentiles, throughput, and error rate, plus how the numbers are produced.]

## 11. Configuration

[Configuration layers in increasing precedence, with defaults.]

| Setting | Flag or variable | Default | Purpose |
|---------|------------------|---------|---------|
| [name]  | [form]           | [value] | [what it controls] |

[Behavior on invalid or unknown configuration.]

## 12. Inner-Loop Dev Process

[The repeatable local loop a contributor runs. Number the steps. State what the README must document verbatim so a fresh clone on a clean machine can reach a working state.]

## 13. Security

[Runtime posture, network restrictions, secret handling, and dependency scanning.]

## 14. Acceptance Criteria

Checkable statements that define done. Each maps to a goal in section 2. Anything not checkable here is not a requirement.

* [ ] [criterion]
* [ ] [criterion]
* [ ] [criterion]

## 15. Open Questions

Items no source note answered, and which are not deliberate choices from section 1.1. Each needs an owner and a resolution path. Blocking items must be resolved before implementation depends on them.

| Question | Blocking | Owner | Source gap |
|----------|----------|-------|------------|
| [what is undecided] | [yes or no] | [who decides] | [which note fell short] |

## 16. Provenance

Where this PRD came from, so a reader can audit any statement.

| Source note | Date | Contributed |
|-------------|------|-------------|
| [path]      | [date] | [sections it informed] |
