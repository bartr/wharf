---
title: Frontier Engineering train-the-trainer pilot
description: Draft facilitator plan for discovering, implementing, and operating a URL-shortener specification
---

# Frontier Engineering train-the-trainer pilot

**Status:** Draft for review, not an approved delivery commitment or a
validated certification standard.

## 1. Purpose and design basis

Train facilitators to guide teams through three distinct stages of
AI-assisted software engineering:

1. **Getting to the spec:** turn an idea into evidence-backed requirements.
2. **Implementing the spec:** build and qualify a bounded release.
3. **Operating the spec:** observe the release, diagnose failures, and compare
   actual behavior with its acceptance criteria.

The pilot starts in a blank repository, installs the FE skills, and explores
an aka.ms-style URL shortener. Each stage retains the evidence needed by the
next. Learners practice the method first, then practice teaching it.

### Agreed direction from the planning discussion

The following captures the 2026-10-28 discussion, not independent research:

* The SDLC is taught as the three stages above.
* The first exercise uses a familiar scenario before bring-your-own-scenario.
* A blank repository and installed skills precede brainstorming.
* Requirements travel with their sources and decisions, not just a final spec.
* A specification template seeds implementation; an implementation template
  seeds operations.
* Emphasis and entry point vary by audience.
* Best practices remain, but teams can adapt their form to their own tools.
* Wharf is an example of what is possible, not a component of the offering.

The one-day agenda, core feature cut, roles, drills, and assessment below are
**proposals**. Delivery duration, stack, cohort size, infrastructure, and
commercial packaging remain open.

## 2. Outcomes

By the end, a trainer should be able to:

* Separate an outcome from a proposed solution and identify unsupported claims.
* Explain the different jobs of a BRD, PRD, and JSONL requirements ledger.
* Trace an acceptance criterion back to a source and forward to a test.
* Facilitate a framed Sessions/RPI cycle without letting the LLM skip ahead.
* Diagnose a bounded operating failure using retained evidence.
* Distinguish application defects from bad requirements and bad measurements.
* Use an LLM as a questioning coach rather than an unquestioned authority.
* Adapt the workflow while preserving its evidence and validation principles.

Successful lab completion is not yet "Frontier Engineering Certified."
Certification needs an independently assessed unfamiliar exercise and pilot
evidence that the assessment measures useful transfer.

## 3. Audience-specific entry points

| Audience emphasis | Start with | Primary exercise | Handoff |
|---|---|---|---|
| Product, business, architecture | Blank repo and stakeholder brief | Discover and specify the problem | Approved spec plus evidence |
| Software engineering | Reviewed specification template | Implement and qualify a release | Runnable implementation plus evidence |
| Platform, SRE, operations | Reviewed implementation template | Diagnose, recover, and explain behavior | Incident record and verified recovery |
| Trainers | Experience all three | Teach back a selected transition | Facilitation feedback and adaptation plan |

These are emphasis choices, not rigid job-role boundaries. Every group sees
what it receives from the preceding stage and owes to the following one.

## 4. Preparation and roles

### Facilitator preparation

Before delivery:

* Rehearse the full route from blank repo through both template handoffs.
* Select and record a tested language/runtime, skill release, CLI/tool versions,
  and deployment environment. Do not select a stack mid-session.
* Prepare a synthetic stakeholder brief containing competing needs, explicit
  constraints, and a few intentional gaps. Label it as workshop material.
* Prepare reviewed reference specification and implementation templates.
  Keep the reference solution out of the learner's initial context.
* Test each drill, its recovery procedure, and its maximum resource use.
* Confirm GitHub repository/template permissions and AI tool access.
* Set spending and resource limits; provide a local fallback for network or
  provisioning failures.
* Confirm that repositories contain no credentials, client information, or
  assessor-only material before they become templates.

Use the maintained [skills distribution instructions](https://github.com/bartr/skills/blob/main/docs/design.md#installing-with-gh-skill).
Record the installed release and any local modifications. Verify that the
assistant can discover the skills; files being present is not sufficient.

### Working roles

Use pairs or small groups, with group size confirmed during the pilot:

* **Learner/driver:** makes predictions and explains decisions before asking
  the LLM to implement.
* **Reviewer/observer:** checks sources, scope, and evidence; records drift.
* **Facilitator:** plays the stakeholder, probes reasoning, and enforces gates.

Rotate roles. The delivery capacity required per facilitator must be measured
in the pilot, not assumed from the availability of AI coaching.

## 5. Proposed one-day agenda

This is an ambitious pilot schedule for a deliberately small, rehearsed
implementation. It is not a promise to build a production URL service in a
day. If rehearsal cannot satisfy the gates, expand to two days or reduce
scope explicitly; do not silently lower the acceptance criteria.

| Time | Block | Exit evidence |
|---|---|---|
| 09:00-09:20 | Orientation, blank repo, skills, roles | Working environment and recorded tool/skill versions |
| 09:20-10:45 | Stage 1: discovery, BRD, PRD readiness, PRD/JSONL | Sources, decisions, readiness review, requirements |
| 10:45-11:00 | Break | |
| 11:00-11:30 | Gate A and spec-template handoff | Reviewed spec snapshot and provenance |
| 11:30-12:15 | Stage 2: frame, research, plan, fit check | Bounded implementation plan |
| 12:15-13:00 | Lunch | |
| 13:00-14:15 | Stage 2: implementation and review | Tests, runnable release, review |
| 14:15-14:45 | Gate B and implementation-template handoff | Qualified operating baseline |
| 14:45-15:00 | Break | |
| 15:00-16:00 | Stage 3: one operating drill and recovery | Hypothesis, evidence, incident record |
| 16:00-16:45 | Trainer teach-back and unfamiliar variation | Facilitation observations and feedback |
| 16:45-17:00 | Retrospective and next steps | Lessons, gaps, and follow-up owners |

The implementation block contains 120 focus minutes, split around lunch.
The facilitator must re-establish the frame on return. Preserve findings in
files and follow the selected skill's phase handoff/context-reset rules.

## 6. Stage 1: getting to the spec

### Exercise

Begin with "We want an aka.ms-style URL shortener," not a finished feature list.
This is a fictional learning service, not an implementation of Microsoft's
actual aka.ms requirements or infrastructure.

1. Save the original brief and brainstorming notes before generating documents.
2. Explore who needs the service, what outcome matters, and alternatives.
3. Use Design Thinking when the problem or users are uncertain. Workshop
   role-play is simulated evidence, not proof of real customer demand.
4. Use the BRD skill to extract business outcomes, measures, scope, risks,
   provenance, and gaps from the written notes.
5. Update the source notes with stakeholder decisions; do not let the LLM
   silently promote proposed assumptions into requirements.
6. Use the PRD skill's readiness review. Resolve blocking gaps before final
   generation.
7. Generate the human PRD and its JSONL ledger from the same source claims.

The BRD is not the JSONL generator. The PRD skill owns the machine-readable
requirements ledger and its validation.

### Proposed bounded release

Confirm or reject this feature cut in the source notes:

* Create a short link for a validated HTTP(S) destination.
* Resolve a short code and return the agreed redirect response.
* Define malformed input, unknown code, collision, and persistence behavior.
* State the redirect/cache policy and how it will be tested.
* Include enough telemetry and a runbook to verify those requirements.

Use only a disposable local environment and synthetic destinations. No real
aka.ms/bit.ly domains, customer links, or public deployment. A local exercise
without authentication is not a production-ready public redirect service.

Discuss identity, ownership, abuse handling, expiry, aliases, and click
analytics, but explicitly defer them or revise the scope and timing. If a
group includes click analytics, define what a "click" means and how caching,
bots, and previews affect measurement.

### Prompt

```text
Discovery only. Treat "an aka.ms-style shortener" as a proposed solution.
Help us identify users, outcomes, constraints, and competing options.
Separate simulated stakeholder evidence from assumptions. Preserve our raw
notes. Do not implement or turn an unanswered question into a requirement.
```

### Gate A: ready to implement

Proceed only when:

* Outcomes, non-goals, and the release boundary are explicit.
* Blocking contradictions and unconfirmed assumptions are resolved.
* Interface, persisted-data, security, and acceptance decisions are stated
  or deliberately delegated with a reason.
* Numerical targets have an agreed measurement method.
* PRD and ledger agree, with source paths/headings on claims.
* Every JSONL line parses; record IDs are unique, referenced IDs exist, and
  the selected skill's record structure and ordering are respected.
* A facilitator can trace one requirement to its source and acceptance test.

The [PRD skill](https://github.com/bartr/skills/blob/main/skills/prd/SKILL.md)
defines the detailed readiness and ledger contract; do not invent a
competing workshop format.

## 7. Stage 2: implementing the spec

Create a new repository from the accepted specification template. Start a
framed session: goal, out of scope, failure condition, and start time.

Run Research, Plan, Implement, Review with durable artifacts. At the fit
check, cut optional scope or use the reference baseline if necessary.
Research and planning must verify APIs and repository patterns before code.
Review must test the actual runnable artifact, not just generated source.

### Prompt

```text
Read the approved PRD, JSONL ledger, source decisions, and repository skills.
Start with research only. Identify the smallest release satisfying the
approved boundary and trace its tests to acceptance criteria. Report gaps
rather than inventing answers. Do not implement until the plan and fit check
are accepted.
```

### Gate B: ready to operate

The implementation template contains:

* The inherited spec, sources, decisions, and provenance.
* Runnable code with reproducible build/start instructions.
* Acceptance and regression tests tied to the agreed requirements.
* A qualified release snapshot and truthful review results.
* A runbook for start, stop, health checks, diagnostics, and recovery.
* Useful logs/metrics, a bounded test workload, and expected baseline behavior.
* Safe, rehearsed drill controls documented separately for facilitators.
* Tracking evidence, session log, known limitations, and no credentials.

Do not pass the gate on "the LLM says it works." Demonstrate the artifact
from a clean instance of the generated repository.

## 8. Stage 3: operating the spec

Start another repository from the reviewed implementation template. First
verify normal behavior against the spec. Then assign one drill.

| Proposed drill | Learner question | Evidence to retain |
|---|---|---|
| Storage dependency unavailable in the disposable lab | Is this a failure of implementation, environment, or an unspecified requirement? | Timeline, logs, health/error behavior, recovery checks |
| Bounded request burst | Which agreed target is missed, and what is the bottleneck? | Workload settings, resource use, latency/error evidence |
| Redirect/cache-policy mismatch | Does the actual response honor the agreed policy? | Response headers, client behavior, acceptance-test result |

For the first pilot, storage unavailability is the simplest default drill.
Load and caching drills require a rehearsed measurement setup; they are not
mandatory features to add during the workshop.

Use a learner's own qualified implementation when its behavior supports the
drill. Otherwise use the reference template and record the switch. Any fault
control is lab-only, off by default, bounded, and must not contaminate the
normal release baseline. Do not plant exploitable vulnerabilities.

### Coaching sequence

1. Learner predicts behavior and writes an initial hypothesis.
2. Facilitator exposes the symptom without naming the cause.
3. Learner requests evidence and describes a discriminating test.
4. An LLM coach asks questions rather than supplying a diagnosis.
5. Learner chooses a recovery, then verifies it against the acceptance criteria.
6. Group compares its reasoning with the facilitator's evidence and records
   what it would do differently.

```text
Act as a questioning senior-engineer coach. Ask one question at a time.
Before giving your view, ask for my hypothesis, evidence, and confidence.
Distinguish observed facts from assumptions. Do not invent logs, fix the
service for me, or reveal the seeded cause before I commit to a diagnosis.
```

Retain an incident record before cleanup: configuration and version identity,
symptoms, timestamps, hypotheses, evidence, actions, recovery verification,
and unresolved questions. A fault injected by the facilitator is marked
synthetic. Never label an unverified recovery as successful.

If the spec is wrong or incomplete, record that finding and return to Stage
1; if implementation contradicts it, return to Stage 2. Operating the spec
includes challenging it, not just keeping the process running.

## 9. Template chain and reference fallbacks

Names below are placeholders; no repositories are created by this plan.

| Snapshot | Contains | Next use |
|---|---|---|
| `shortener-spec` | Skills, original sources, discovery/BRD, readiness review, PRD/JSONL | Create an implementation repo |
| `shortener-implementation` | Spec evidence plus qualified code, tests, runbook, operating baseline | Create an operations repo |
| Operations evidence snapshot | Implementation plus incidents, recoveries, lessons | Create a knowledge pack; optional third template |

GitHub templates copy files, not the original commit history. Commit the
evidence files before using a template, and record the source repo, release
tag, resolved commit SHA, installed skill versions, and known limitations in
each generated repo. Confirm the generated files correspond to that recorded
snapshot; do not assume creating from a moving default branch selects a tag.

Keep both cohort-produced and reviewed reference snapshots. At a failed gate:

1. Retain the cohort's work and the specific reason the gate failed.
2. Allow a bounded correction if the schedule supports it.
3. Otherwise continue from the reference template and record the handoff.
4. Debrief the gap later; do not count reference-template success as independent
   completion of the preceding stage.

This lets the next stage proceed without hiding how the cohort actually did.

## 10. Trainer teach-back and pilot assessment

In the teach-back, trainers facilitate rather than demonstrate their own speed.
Assign a colleague an unfamiliar variation, such as a new ownership requirement
or an unexplained error-rate increase. The trainer must ask useful questions,
preserve uncertainty, identify the correct SDLC stage, and choose a safe next
step without taking over the keyboard.

### Proposed observation rubric

| Dimension | Evidence |
|---|---|
| Discovery and traceability | Separates assumptions from evidence; traces requirement to source and test |
| Implementation judgment | Maintains scope, validates APIs, qualifies the actual artifact |
| Operating judgment | Tests hypotheses safely, retains evidence, verifies recovery |
| Facilitation | Coaches without giving answers; knows when and how to use a fallback |
| Transfer and adaptation | Handles an unfamiliar variation while preserving the principles |

Score each dimension: **0** no evidence, **1** completes with substantial help,
**2** independently demonstrates it, **3** explains trade-offs and can teach it.
Proposed pilot completion requires at least 2 in each dimension. The threshold
is provisional, not validated; a human assessor owns the judgment.

Save initial predictions separately from final answers. Track time spent,
coaching interventions, fallback use, and unsupported claims that were caught
or missed. Use the pilot to calibrate timing, facilitator capacity, rubric,
and later transfer/retention assessment. LLM feedback alone is not certification.

## 11. What remains to decide

| Decision | Proposed owner | Blocking when |
|---|---|---|
| One-day versus extended delivery; core feature cut | Curriculum lead | Before rehearsal |
| Stack, local runtime, costs, and access prerequisites | Technical facilitator | Before rehearsal |
| Reference templates and safe drill implementation | Lab maintainer | Before delivery |
| Cohort size and facilitator coverage | Delivery lead | Before registration |
| Skill release and allowed client adaptations | Curriculum lead | Before publishing templates |
| Success measures and independent assessment process | Assessment lead | Before making certification claims |
| Template ownership, maintenance, branding, and licensing notices | Program owner | Before external distribution |

## 12. Follow-on labs and materials

* **Helium:** a familiar service with an existing spec and data, suitable for
  brownfield implementation and deeper operating/performance exercises.
* **Bring your own scenario:** follows the known exercise; the client's own
  evidence becomes its own curriculum.
* **Wharf:** a reference case demonstrating the method under greater
  operational complexity, not software participants must deploy.
* [Wharf knowledge pack](knowledge/README.md), [failure labs](knowledge/failure-labs.md),
  and [LLM coach prompts](knowledge/llm-coach-prompts.md): examples to adapt,
  not mandatory client tooling.

The underlying [skills](https://github.com/bartr/skills) provide the reusable
method. This plan adds facilitation, handoff gates, and a pilot structure; it
does not replace their templates or claim that a finished curriculum,
reference implementation, or certification program already exists.
The RPI pattern is credited to [Microsoft HVE Core](https://github.com/microsoft/hve-core)
by the shared RPI skill.
