# [Initiative Name] Business Requirements

Status: [Draft v0.1]
Date: [YYYY-MM-DD]
Sponsor: [who is accountable for the outcome]
Author: [name]
Sources: [input notes this BRD was built from]

Replace every bracketed placeholder. Keep every section heading. When a section does not apply, keep the heading and write "Not applicable" with a one-line reason.

This document states the business need and the outcomes that define success. It does not specify a solution. Once approved, it becomes the input to a PRD.

Label any statement not directly supported by a source note:

* `derived` means inferred from the notes and awaiting confirmation.
* `open` means no source addresses it and a decision is needed.
* `conflicting` means sources disagree and the disagreement is unresolved.

## 1. Summary

[Three to five sentences a sponsor can read on their own. What business problem or opportunity this addresses, who it affects, what changes if it succeeds, and what it will take.]

## 2. Business Problem or Opportunity

[The problem in business terms: lost revenue, cost, risk, delay, quality, capability gap, or unmet demand. Describe the current cost of doing nothing. Avoid naming a solution here.]

## 3. Stakeholders

| Stakeholder | Role in this initiative | Interest or concern | Decision rights |
|-------------|-------------------------|---------------------|-----------------|
| [name or group] | [sponsor, owner, affected team, customer] | [what they need from this] | [approves, consulted, informed] |

[Name who decides, who funds, and who is affected but not represented.]

## 4. Business Objectives

Numbered, outcome-shaped, and measurable. Each objective is traced by at least one requirement in section 8.

| ID | Objective | Why it matters |
|----|-----------|----------------|
| BO-1 | [outcome] | [business consequence] |
| BO-2 | [outcome] | [business consequence] |

## 5. Success Measures

How anyone will know this worked, stated so the result can be disputed with data.

| Objective | Measure | Baseline today | Target | How measured | By when |
|-----------|---------|----------------|--------|--------------|---------|
| BO-1 | [metric] | [current value] | [target value] | [source of truth] | [date] |

[When a baseline is unknown, mark it `open` and make establishing it a requirement rather than guessing.]

## 6. Current State

[How the work happens today, including workarounds, manual steps, and existing tools. This is what the initiative is measured against.]

## 7. Scope

In scope:

* [capability or audience]

Out of scope:

* [explicit exclusion, and why]

[Name the tempting adjacent work that is excluded, so scope debates are settled here rather than during delivery.]

## 8. Business Requirements

What the business needs to be true. Each requirement states a need and its outcome, not an implementation.

| ID | Requirement | Objective | Priority | Notes |
|----|-------------|-----------|----------|-------|
| BR-1 | [the business needs ...] | BO-1 | [must, should, could] | [constraints or label] |
| BR-2 | [the business needs ...] | BO-2 | [must, should, could] | [constraints or label] |

## 9. Constraints

[Budget, timeline, staffing, regulatory, contractual, platform, or policy limits that bound any solution.]

## 10. Assumptions

[What is being taken as true without proof. Each assumption names what happens if it turns out false.]

## 11. Risks

| Risk | Impact | Likelihood | Mitigation | Owner |
|------|--------|------------|------------|-------|
| [what could go wrong] | [business consequence] | [high, medium, low] | [response] | [name] |

## 12. Dependencies

[Teams, systems, vendors, approvals, or other initiatives this depends on, and what is needed from each.]

## 13. Options Considered

| Option | Summary | Cost or effort | Trade-off |
|--------|---------|----------------|-----------|
| [including do nothing] | [description] | [rough sizing] | [what is given up] |

Recommendation: [which option and why, in one paragraph tied to the objectives in section 4.]

## 14. Open Questions

| Question | Blocking | Owner | Source gap |
|----------|----------|-------|------------|
| [what is undecided] | [yes or no] | [who decides] | [which note fell short] |

## 15. Approval

| Approver | Role | Decision | Date |
|----------|------|----------|------|
| [name] | [sponsor, finance, legal] | [approved, rejected, pending] | [date] |

## 16. Provenance

| Source note | Date | Contributed |
|-------------|------|-------------|
| [path] | [date] | [sections it informed] |
