---
title: Assessment answer key
description: Assessor-only answers, rationale, evidence, and scoring rubric for the Wharf assessments
---

## Assessor use only

Do not distribute this file with the assessments.

## Assessment 1: Foundations

Score one point per multiple-choice answer and up to two points per short
answer: one for the correct principle and one for the reasoning or evidence.
The maximum is 18; 80% is 14.4, so 15 points passes.

| # | Answer | Rationale | Evidence |
|---|---|---|---|
| 1 | C | PASS@1 uses every retained logical trial, including failed, timed-out, and cancelled ones. | [Lesson 9](lessons.md#9-missing-is-not-zero-and-the-denominator-is-planned) |
| 2 | B | Verifier failures stay inside the verifier-only retry boundary. | [Lesson 3](lessons.md#3-retries-must-never-change-what-you-are-measuring) |
| 3 | C | A clean exit with no patch is terminal `AgentUnfinished`. | [Lesson 3](lessons.md#3-retries-must-never-change-what-you-are-measuring) |
| 4 | C | The cold case is the worst case; higher warm `P` saves about a minute. | [Lesson 1](lessons.md#1-scale-up-before-you-scale-out) |
| 5 | B | Missing costs are never imputed, including from batch bills. | [Lesson 9](lessons.md#9-missing-is-not-zero-and-the-denominator-is-planned) |
| 6 | C | The 1.1.0 package passed metadata checks but was missing its DLL. | [Lesson 7](lessons.md#7-test-the-deliverable-not-its-description) |

**7.** `N` limits concurrent trials and is mostly bounded by CPU and memory
while agents run. `P` limits preparation work: image pulls and builds, CLI
payload setup, workspace creation, and patch application. It protects the
Docker daemon, storage driver, and disk from I/O bursts. Full credit requires
both resources and the reason they are independent.

**8.** Other waiters depend on the same build. If one caller's cancellation
ended the shared work, every duplicate request would fail. Cancellation
belongs at the wait boundary, and failed builds are removed from the
in-flight map so they can be retried.

**9.** A slow telemetry sink would otherwise keep the slot looking occupied,
blocking new work and undermining work-conserving scheduling.

**10.** Those checks could reward implementations that echoed prompt details
without proving behavior. Executable contract tests verify behavior.

**11.** The suites differ in tasks, languages, size, cost, and difficulty. They
are separate datasets with separate manifests, so their pass rates are not
interchangeable.

**12.** Record run identity and configuration (versions, model, `K`, `N`, `P`),
host and cache state, results, exact errors, telemetry, and diagnosis, in
`.github/tracking/`. The failed run is often the only evidence of why a limit
or fix exists.

## Assessment 2: Scenarios

Score each scenario from 0 to 4 using the rubric, for a maximum of 12. A
passing result is 9 or higher, with no scenario scored 0.

| Score | Meaning |
|---|---|
| 4 | Correct decision, clear reasoning, and specific evidence to cite or collect |
| 3 | Correct decision and reasoning; evidence is general |
| 2 | Partially correct decision, or correct decision without reasoning |
| 1 | Identifies the issue but recommends an unsafe or unsupported action |
| 0 | Incorrect or missing |

### Scenario 1: expected elements

* The likely cause is preparation I/O overwhelming Docker storage on a cold
  run, because `P` was too high, not because `N` was.
* Recommend reducing `P` to a conservative value sized for the cold case and
  keeping it for every run. A reasonable starting point is near the z01
  ratio, about `P=12` per 16 cores scaled with storage capacity, while `N`
  can stay near 2× CPU. The enterprise suite has only 56 trials, so `N` above
  56 adds nothing.
* Record a tracking entry with configuration, host, cold-cache state, elapsed
  time, errors, and Docker logs before deletion.
* Show preparation-gate telemetry: queue waits are measured in seconds, while
  the failure cost a whole run.

### Scenario 2: expected elements

* Two single runs cannot establish a causal regression. Model behavior and
  service conditions can change between runs. P2P-error trials are worth
  investigating.
* Collect repeated runs of both versions under controlled conditions, check
  the specific P2P failures, and confirm the evaluator revision is the same.
* Describe it as an observed difference that warrants investigation, not a
  confirmed regression.

### Scenario 3: expected elements

* Not proven. Accepting and rendering a setting does not prove execution
  consumes it, as the 1.0.0 review found.
* Add a test that runs execution with the override and asserts the effective
  timeout at the agent boundary, including precedence over task values.
* Execute the packaged artifact, and check filesystem and Docker residue after
  failure paths. An independent code review also counts.
