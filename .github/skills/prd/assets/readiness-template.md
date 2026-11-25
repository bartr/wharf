# [Product] PRD Readiness Review

Status: [READY or NOT READY]
Date: [YYYY-MM-DD]
Sources: [all reviewed source paths]
Planned PRD: [destination]
Planned LLM ledger: [destination and selected format]

## 1. Source Inventory

| Source | Date | Authority | Notes |
|--------|------|-----------|-------|
| [path] | [date] | [primary, supporting, existing PRD] | [scope] |

## 2. Coverage by PRD Section

| PRD section | Coverage | Evidence | Missing |
|-------------|----------|----------|---------|
| [section] | [complete, partial, absent, deliberate] | [source heading] | [specific gap] |

## 3. Blocking Gaps

| ID | Missing decision | Why it blocks | Proposed assumption | Add decision under |
|----|------------------|---------------|---------------------|--------------------|
| G-001 | [gap] | [consequence] | [suggested default, not yet a requirement] | [source heading] |

## 4. Non-Blocking Gaps

| ID | Missing decision | Consequence | Proposed assumption | Add decision under |
|----|------------------|-------------|---------------------|--------------------|
| G-101 | [gap] | [consequence] | [suggested default] | [source heading] |

## 5. Proposed Assumptions

| ID | Status | Assumption | Rationale | Risk if wrong | Source confirmation |
|----|--------|------------|-----------|---------------|---------------------|
| A-001 | proposed | [assumption] | [why this default] | [impact] | [heading to update] |

Statuses: `proposed`, `confirmed`, `rejected`, or `deliberate`.

## 6. Conflicts

| ID | Position A | Position B | Preferred source | Resolution needed |
|----|------------|------------|------------------|-------------------|
| C-001 | [claim and source] | [claim and source] | [most recent] | [decision] |

## 7. Deliberately Unspecified Candidates

| ID | Decision | Why implementation should own it | Confirmation needed |
|----|----------|----------------------------------|---------------------|
| D-001 | [decision] | [reason] | [yes until source says deliberate] |

## 8. Acceptance-Criteria Readiness

| Goal or requirement | Measurable now | Missing measurement or threshold |
|---------------------|----------------|----------------------------------|
| [claim] | [yes or no] | [gap] |

### 8.1 Evaluation-plan readiness

Use the [shared evaluation contract](../../evaluation-contract.md).

| Element | Source/confidence | Missing evidence and resolution path | Blocks readiness |
|---|---|---|---|
| User outcome and domain constraint | [source] | [gap or none] | [yes/no with reason] |
| Baseline and improvement hypothesis | [observed/assumed/unknown with source] | [next observation; do not invent measurements] | [yes/no with reason] |
| Evaluation method and comparison conditions | [source or confirmed decision] | [synthetic/integration/live checks and confounders] | [yes/no with reason] |
| Non-regression and improvement decision criteria | [source or confirmed decision] | [acceptance method and required evidence] | [yes/no with reason] |

Record assumptions the evaluation could revise in existing gaps or open
questions. READY means ready to build with a sufficient evaluation plan, not
that the improvement is proven. An unknown baseline is blocking only when an
essential requirement or acceptance decision depends on resolving it first.

## 9. LLM Artifact Recommendation

- Recommended format: [JSONL or structured Markdown]
- Reason: [claim volume, atomicity, relationships, update pattern]
- Planned record types: [metadata, goal, requirement, constraint, decision,
  non_goal, acceptance_criterion, deliberate, open_question]
- Validation required: [JSON parse, unique IDs, valid references, provenance]

## 10. Readiness Verdict

- Verdict: [READY or NOT READY]
- Blocking gaps: [count]
- Non-blocking gaps: [count]
- Conflicts: [count]
- Proposed assumptions awaiting source confirmation: [count]
- Next action: [update source notes or generate final artifacts]
