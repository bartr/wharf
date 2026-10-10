# Draft meeting brief: DeepSWE and the GitHub Copilot agentic harness

## Purpose

Discuss how our independent DeepSWE work complements GitHub's agentic-harness
evaluation program and identify joint experiments that can improve Copilot's
task resolution, model selection, efficiency, and evaluation telemetry.

## Executive summary

Our work independently supports the central premise of GitHub's
[agentic-harness evaluation
post](https://github.blog/ai-and-ml/github-copilot/evaluating-performance-and-efficiency-of-the-github-copilot-agentic-harness-across-models-and-tasks/):
the useful unit of evaluation is an **agent-model configuration**, not the
model alone. The harness materially changes both task resolution and cost.

On 113 DeepSWE tasks across five languages:

- **Opus harness test:** Claude Opus 5 with Copilot reached **68.5%**, compared
  with **64.4%** for Claude Code, while average cost per trial fell from
  **$13.40 to $5.77**.
- **Luna harness test:** GPT-5.6 Luna with Copilot reached **72.8%**, versus
  **67.2%** with the DeepSWE mini-swe-agent harness, while average cost per
  trial fell from **$3.03 to $0.37**.
- **Terra product-harness test:** GPT-5.6 Terra with Copilot passed **376/565
  (66.5%)**, versus Meta's rounded **64.8%** for Terra with Codex, consistent
  with approximately **366/565**. The nominal ten-pass Copilot lead is smaller
  than the local run's task-clustered variability.
- **Auto test:** Copilot Auto passed **209/565 (37.0%)**. Hydrafusion should be
  evaluated with the same K=5 design when it becomes available.
- **Muse corroboration test:** Muse Spark 1.2 with Muse Code passed **338/565**,
  versus Meta's rounded headline consistent with **335/565**. A difference of
  three passes across 565 attempts is effectively tied relative to DeepSWE's
  observed trial-to-trial variability.

The opportunity is to combine GitHub's harness expertise and product telemetry
with our reproducible, task-level, multi-language evaluation to diagnose *why*
configurations move on the quality-cost frontier, not just whether they move.

## How our approach fits GitHub's approach

| GitHub evaluation principle | How our work complements it |
| --- | --- |
| Evaluate the harness, not just the model | We compare model-vendor harnesses and Copilot using complete agent-model configurations. |
| Hold model and task fixed for harness comparisons | Our Opus 5 comparison isolates Copilot versus Claude Code on the same DeepSWE corpus. |
| Measure task resolution and efficiency together | We report PASS@1, cost per task, runtime, token/credit telemetry, and cost per pass where coverage permits. |
| Account for stochastic variation | We run repeated trials per task, retain failures in the denominator, and preserve trial-level outcomes. |
| Distinguish model failures from infrastructure anomalies | We classify agent failures, agent timeouts, verifier timeouts, and content-policy blocks rather than silently rerunning everything. |
| Use multiple models | We have evaluated Claude, OpenAI, and Muse configurations through their selected harnesses. |
| Complement benchmarks with practical evidence | DeepSWE adds 113 repository-level tasks in Go, Python, TypeScript, JavaScript, and Rust with functional and regression verifiers. |

GitHub's published evaluation uses different benchmark suites, so its numerical
results should not be compared directly with ours. The alignment is in the
experimental model and terminology. The numerical corroboration is between our
DeepSWE results and Meta's DeepSWE aggregates.

## What the results suggest

### 1. Harness quality and harness efficiency are both material

Copilot improved aggregate Opus 5 task resolution by 4.1 percentage points
relative to Claude Code while reducing average cost per trial by about 57%.
The quality change is useful; the cost change is the larger product result.
The harness appears to extract more completed work from substantially less
spend.

### 2. Model choice inside Copilot can move the quality-cost frontier

The same-model comparison is strong: Luna with Copilot scored 72.8%, versus
67.2% for Luna with the DeepSWE mini-swe-agent harness, while costing 87.9%
less per trial ($0.37 versus $3.03). Luna with Copilot also exceeded Opus 5 with
Copilot by 4.3 percentage points while costing 93.6% less.

Meta's selected-product-harness result provides another useful reference:
GPT-5.6 Terra with Codex scored 64.8%. Luna with Copilot scored 8.0 percentage
points higher. Because both the model and harness change, this is not a causal
harness comparison, but it is closer to how users experience the products than
a comparison against a mini-swe-agent leaderboard arm.

More importantly, our existing Terra + Copilot arm is already K=5: 376/565
(66.5%). Meta's rounded 64.8% Terra + Codex result is consistent with 366/565.
This holds the model, task suite, and K fixed while changing the harness and
evaluation infrastructure, making it the closest external analogue to GitHub's
same-model harness comparisons. The ten-pass, 1.77-point nominal Copilot lead
is smaller than the local Terra run's 3.29-point task-clustered standard error,
so the practical conclusion is that the harnesses are close on this
configuration.

As secondary context, Luna with Copilot tied the 72.8% DeepSWE Opus 5
mini-swe-agent reference. Taken together, the results suggest that routing and
model choice should optimize for expected task resolution per unit of cost,
not model prestige or raw capability alone.

### 3. The current Auto result shows substantial room for improvement

The tested Auto arm passed 209/565 trials, or 37.0%. Removing its 27 agent
timeouts from the denominator raises the rate only to 38.8%, so infrastructure
failures do not explain the gap. The export records `model: auto`, but not the
model selected for each trial; consequently, we cannot determine whether the
failure mode is routing, model capability, effort allocation, tool behavior,
or an interaction among them.

This is a result for one Auto implementation on DeepSWE, not evidence that
automatic selection is inherently ineffective. Hydrafusion gives us a clean
opportunity to test whether the new approach closes the gap.

### 4. Independent aggregate agreement supports the methodology

Our Muse Spark 1.2 + Muse Code result is 338/565. Meta's rounded 59.3%
headline is consistent with 335/565: only three passes across 565 attempts.
Our Opus 5 + Claude Code result is 291/452; Meta's rounded 65.0% K=5 result is
consistent with 367/565, while our rate scales to about 364 pass equivalents on
that denominator.

Meta did not publish exact pass counts or task-level rows, so its numerators
are inferred from rounded headlines and this is corroboration rather than a
paired replication. At the available precision, both results are effectively
tied; the approximately three-pass difference is an intuitive external check
on the methodology.

The DeepSWE outcome variance makes that small difference even less meaningful.
In our K=5 Muse run, 65/113 tasks had mixed outcomes between 1/5 and 4/5 rather
than landing at 0/5 or 5/5. The three-pass gap is 0.53 percentage points,
compared with a 2.1-point naive trial-level standard error and a 3.3-point
task-clustered standard error in our run. Without Meta's task rows this is not
a formal equivalence test, but “effectively tied” is the practical result.

### 5. Tool and subagent policy is an optimization surface

In a historical matched 40-task Sol comparison, excluding the built-in `task`
tool reduced pass rate from 80.0% to 77.5% while reducing total cost by 41.1%.
That pilot should not be generalized, but it demonstrates the value of
measuring marginal resolution against marginal cost for individual harness
capabilities.

## Proposed joint work

### 1. Hydrafusion evaluation

Run Hydrafusion on all 113 tasks at K=5, alongside the existing Auto baseline
and strong fixed-model configurations.

Keep timeout, effort, context, tools, MCP availability, verifier, and task
versions fixed. Pin and record the Copilot CLI and harness build. Preserve the
old Auto result rather than replacing it.

Measure:

- PASS@1 and task-level consistency;
- cost per task and cost per successful trial;
- agent time, end-to-end time, and timeout rate;
- selected model per trial and any fallback sequence;
- token and Copilot-credit usage with complete coverage;
- results by language, task difficulty, and task archetype.

### 2. Test context and skill packages

Use the same task-level design to test opt-in instruction or skill packages,
including our HVE integration. Measure whether additional guidance improves
resolution enough to justify its context, token, and latency cost, and identify
the task categories where it helps or harms.

### 3. Agree on a portable evaluation record

Define a small shared schema for external evaluations:

- benchmark, task, trial, and configuration identifiers;
- model, harness, CLI, and policy versions;
- effective context, effort, tools, and subagent settings;
- outcome and failure category;
- token, credit, cost, and timing fields;
- infrastructure anomaly and inclusion flags.

A portable record would make independent results easier to reproduce and would
surface configuration drift before it invalidates a comparison.

## Concrete asks for the meeting

1. **Hydrafusion access:** Can we obtain preview access and a pinned build for
   an independent DeepSWE run?
2. **Evaluation alignment:** Which settings and exclusion policies should we
   mirror to make our controlled arm most comparable with GitHub's internal
   harness evaluations?

## Suggested first experiment

**Question:** Does Hydrafusion materially improve DeepSWE task resolution and
efficiency over the current Auto implementation without regressing the best
fixed-model configurations?

**Design:**

- 113 DeepSWE tasks, K=5;
- current Auto, Hydrafusion, Luna, and one premium frontier model;
- pinned harness and CLI versions;
- identical environment, timeout, effort policy, tools, and verifier;
- routed model and fallback telemetry captured per trial;
- failures retained, with infrastructure anomalies reported separately.

**Primary outputs:**

- PASS@1 with uncertainty;
- task-matched deltas;
- cost per task and per pass;
- run-to-run and task-level consistency;
- routing confusion analysis against the fixed-model outcomes;
- timeout, fallback, and missing-telemetry rates.

Success criteria should be agreed before the run. At minimum, Hydrafusion
should substantially close the current 31.5-to-35.8-point Auto gap while
preserving a meaningful cost advantage. A stronger target is to approach the
best fixed-model quality-cost frontier rather than merely improve on the weak
current Auto baseline.

## Important caveats

- Results are aggregate unless explicitly described as task-matched or paired.
- Meta published aggregate values but not task-level rows for the comparisons
  cited here.
- Existing Copilot arms include known CLI-version drift and some subagent model
  mixing; new controlled experiments should pin versions and inheritance.
- The five JavaScript and five Rust tasks are too few for strong
  language-specific conclusions.
- Incomplete cost telemetry must not be presented as a complete arm cost.

## Desired meeting outcome

Leave with agreement on:

1. the first Hydrafusion experiment and success criteria;
2. the configuration and telemetry needed to make it diagnostic;
3. owners and a review cadence;
4. which findings can be shared publicly and how they should be framed.
