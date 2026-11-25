# Continuous improvement with evaluations

Use this shared contract within existing discovery, PRD, RPI, and session
artifacts. It applies to software systems, harnesses, skills, and operating
practices; the subject does not have to be an agent.

Domain knowledge informs the improvement hypothesis. Evaluation evidence can
also revise that knowledge. Record both directions rather than treating
current expert opinion as a permanent fact.

## The six-part contract

| Part | Capture |
|---|---|
| **Outcome** | The user, their desired outcome, and the domain constraint or problem. |
| **Evidence** | What is observed, assumed, unknown, or conflicting; sources and baseline conditions. |
| **Hypothesis** | Why one targeted change should improve the outcome, and what observation would challenge that explanation. |
| **Evaluation** | A checkable method, relevant comparison, and what must not regress. Include thresholds only when supported or explicitly approved. |
| **Decision** | Keep, revise, reject, or defer the change based on evidence; state limitations and the next check. |
| **Learning** | What changed in domain understanding, the scope of that finding, and what remains uncertain. |

Carry the contract forward by reference; do not re-create six documents or
repeat discovery when adequate evidence already exists. Discovery frames it,
the PRD specifies it, RPI executes and evaluates it, and session close records
the decision and learning.

## Evidence and comparison

- Separate a planned evaluation, completed execution, observed outcome, and
  accepted improvement. A successful command is not proof of user benefit.
- Distinguish deterministic/synthetic checks, integration checks, and live
  user or provider observations. Passing one level does not prove another.
- Cite existing baseline evidence and record relevant conditions. For AI
  engineering, distinguish model, harness, skills, configuration, task set,
  and environment rather than attributing every difference to the model.
- Reuse a suitable evaluation set or record why it must change. Keep relevant
  conditions matched where feasible; disclose confounders and unmatched
  comparisons rather than claiming causality.
- Preserve failures, missing measurements, and the agreed scoring denominator.
  Do not exclude inconvenient outcomes or rerun ordinary failures until passing.
- When a baseline is unavailable, label it unknown and specify the smallest
  useful next observation. Do not invent a baseline, numerical target, cost
  saving, or accuracy gain to complete a template.

## Readiness, decisions, and proportionality

PRD readiness means ready to build with a defined evaluation method and
non-regression criteria, not that the change has already demonstrated benefit.
An unknown baseline is blocking only when it prevents an essential requirement
or acceptance decision; otherwise record its resolution path explicitly.

At review and close, record the disposition and its evidence separately from
release status. If evidence is incomplete, narrow the claim or defer the
improvement decision and name the next check. Live trials, paid benchmarks,
deployment, and release actions still require their normal authorization.

Scale the contract to the task. A bounded bug fix may need only the observed
failure, intended correction, and a regression test in the existing handoff.
It does not need discovery, a numerical experiment, or extra documents.

Keep durable findings in the project's existing learning record with sources,
conditions, and uncertainty. Promote only qualifying, non-sensitive facts to
memory under the applicable memory policy; do not turn one comparison into a
universal ranking.
