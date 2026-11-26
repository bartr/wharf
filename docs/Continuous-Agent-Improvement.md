![Continuous agent improvement with evaluations](Continuous-Agent-Improvement-Evaluations.png)

# Continuous System Improvement with Evaluations: Wharf

The slide uses "agent"; we believe the method is broader. Wharf is a harbor
evaluation platform that demonstrates the loop improves expertise, software,
harnesses, skills, and operating practices.

## Executive takeaway

This journey started with HVE: would its engineering guidance improve
outcomes? We tested that hypothesis, and the findings informed our
recommendation to retain the useful discipline in a much smaller form.
The "skills" used to develop Wharf are a **very slimmed-down subset of HVE**,
packaged as reusable workflows and refined locally, not the full HVE offering.

### The full-circle engineering story

1. **Evaluate HVE.** Test whether the guidance improves engineering outcomes,
   rather than assume that more guidance means better results.
2. **Slim it down.** Turn the findings into a recommendation: retain useful
   HVE discipline in a small, reusable skills subset.
3. **Build a real CLI with those skills.** Use them to develop Wharf, retaining
   requirements, decisions, tests, and operational evidence along the way.
4. **Encounter the slide and recognize the method.** The slide arrived after
   the development work. We recognized its improvement loop as a description
   of how we had already developed and refined Wharf.
5. **Use the repositories as the case study.** Wharf and DeepSWE's retained
   engineering records supplied concrete examples of the loop, including
   successful changes, rejected assumptions, and new domain knowledge.
6. **Feed the learning back into the skills.** Incorporate the slide's
   guidance into our HVE-derived skills through the shared evaluation contract,
   so the next engineering effort can reuse what this one taught us.

This is a **full-circle, 360-degree engineering accomplishment**: evaluation
shaped the skills, the skills helped produce a working platform, the platform's
development supplied evidence, and that evidence improved the skills again.
The outputs are not just a CLI, but also better domain understanding and a
reusable engineering practice. The slide did not cause the original work;
it helped us recognize, explain, and deliberately extend the loop.

Improving an agent-powered engineering system is not limited to selecting a
better model. Outcomes depend on the **model, harness, and skills**, and how
they interact with the user's environment and constraints.

Domain knowledge both informs and benefits from the loop. Expert understanding
helps select a useful change; evaluation can challenge conventional wisdom and
improve that understanding. Findings must retain their tested conditions rather
than becoming universal claims about a model or product.

Wharf's batch execution illustrates the operational value. Understanding
subscription quota windows led to a targeted harness improvement: users can
execute one reproducible benchmark over multiple sessions, retain completed
work, and continue the remaining work after a quota reset.

The demonstrated gain is operational usability and continuity, not a measured
increase in model accuracy. We made the system fit the user's constraints
without changing the benchmark's definition of success.

The way Wharf was developed paid dividends again. Skills guided requirements,
research, planning, implementation, and review; the resulting artifacts now
let us recover the reasoning, evidence, and limitations behind the changes.
That makes the value easier to identify and reuse, rather than leaving it in
the recollection of the engineer who did the work.

The cases below show three kinds of progress: improving the platform, learning
which model-harness-guidance configurations work, and improving the method
used to develop and evaluate them. A useful result can be a retained feature,
a revised assumption, or an actionable finding for a product team.

## HVE: why this journey started

**Outcome and domain insight.** The original purpose of the journey was to
evaluate whether HVE improves engineering outcomes. More guidance consumes
context and may help, do nothing, or interfere; its value should be measured.
We tested the hypothesis rather than treating the presence of HVE as proof
of better engineering.

**Starting point and evaluation.** Historical DeepSWE research tested a small
always-on note plus invokable skills rather than treating an approximately
22K-token force-injected corpus as the representative integration. An early
eight-task Go panel passed **23/32 trials with and without HVE**. Provenance is
retained in the [research catalog](../.github/tracking/2026-11-25-continuous-improvement-candidates-research.md#7-hve-more-guidance-is-a-hypothesis-not-an-automatic-improvement),
including the historical report's commit and line references.

The current retained Copilot 1.0.77 exports provide larger observations on
identical baseline/HVE task-checksum sets, with 452 trials per arm:

| Configuration | Baseline passes | HVE passes |
|---|---:|---:|
| Opus 5 | [322/452](../deep-swe/data/copilot-claude-opus-5-max-1.0.77.jsonl) | [300/452](../deep-swe/data/copilot-claude-opus-5-max-hve-1.0.77.jsonl) |
| GPT-5.6 Luna | [335/452](../deep-swe/data/copilot-gpt-5.6-luna-max-1.0.77.jsonl) | [309/452](../deep-swe/data/copilot-gpt-5.6-luna-max-hve-1.0.77.jsonl) |

**Recommendation and reuse.** These observations did not establish a quality
gain from the tested guidance. The tested hypothesis informed our
recommendation: retain useful HVE engineering practices as a very slimmed-down
skills subset, rather than assume that a larger guidance package delivers
better outcomes. Those skills supplied the discipline used to develop Wharf
and later to identify and reuse its learning.

By "skills" here, we mean this selected subset of HVE's engineering approach,
not every skill in the ecosystem or a wholesale installation of HVE.
The project lead reports that the original campaign has concluded and work
with the HVE team now focuses on "shrinking" HVE. The recommendation is to make
guidance selective and test its marginal value, not equate instruction volume
with quality.

**Evidence boundary.** Historical CLI/subagent confounds and differing
integrations limit causal claims. The recommendation is evidence-informed,
not a measured claim that the slimmed-down skills outperform every tested HVE
integration. HVE-derived skills used to develop Wharf and guidance supplied to
a benchmark agent are distinct uses; success in one does not prove the other
effective. The ongoing HVE reduction work has no measured outcome here.

## Skills paid dividends in development and again in reuse

### How we developed Wharf

Wharf's development used skills to turn domain knowledge into explicit
requirements, bounded work, research, plans, implementation, and review.
These skills are the very slimmed-down HVE subset described above: the
recommendation from the evaluation put into engineering practice.
The [implementation sessions](wharf-sessions.md) describe the RPI loop,
durable artifacts, scope boundaries, and required release evidence.
[RPI](../.github/skills/rpi/SKILL.md), credited to Microsoft HVE Core, separates
investigation from implementation so a plausible answer is not treated as
verified engineering.

Those records have a second use. While assembling this document, we could
trace preparation-concurrency decisions, cache validation, benchmark protocols,
and failure diagnoses back to retained evidence. The
[case-study research](../.github/tracking/2026-11-25-continuous-improvement-candidates-research.md)
identified 18 potential additions, including the examples in this record.

The development method therefore produced more than a working platform: it
left a record we can reuse to explain decisions, challenge assumptions, and
transfer practices to other work. This is an observed reuse of the artifacts,
not a controlled estimate of how much faster or cheaper the skills made
development.

### The improvement hypothesis

The batch specification already defined precise counting, recovery, and
measurement rules. Reviewing its live execution exposed a useful distinction:
requirements can be ready to implement while evidence for an improvement is
still partial. A successful batch invocation does not establish every quota
or post-reset behavior.

The hypothesis is that making this distinction explicit in our existing
engineering skills will improve framing, evaluation planning, and the accuracy
of completion claims without adding another document-heavy workflow.

### The targeted change

Added a [shared evaluation contract](../.github/skills/evaluation-contract.md)
with six parts: **outcome, evidence, hypothesis, evaluation, decision, and
learning**. It is carried through existing artifacts, not six new documents.

| Existing skill | Reuse of the slide |
|---|---|
| [Design thinking](../.github/skills/design-thinking/SKILL.md) | Frame the user outcome and domain constraints; separate evidence from assumptions; test the concept and record what discovery taught us. |
| [PRD](../.github/skills/prd/SKILL.md) | Specify the hypothesis, baseline or explicit unknown, comparison method, required evidence levels, and what must not regress. READY means ready to build, not proven effective. |
| [RPI](../.github/skills/rpi/SKILL.md) | Carry the criteria into implementation and review; separate executed checks from observed benefit; decide keep, revise, reject, or defer with evidence. |
| [Sessions](../.github/skills/sessions/SKILL.md) | Record the improvement decision, learned domain knowledge, limitations, and the next observation alongside release status. |

Updated the existing PRD, readiness, requirements-ledger guidance, and session
log templates so generated artifacts preserve the contract. Installation
metadata records these as local customizations; this does not change the
upstream skills repository.

### What stays invariant

- Source provenance and explicit assumptions remain mandatory.
- Unknown baselines stay unknown; no invented savings, rankings, or numerical
  targets are introduced to fill a template.
- Synthetic checks, integration checks, and live observations are distinct.
- Failures and missing measurements remain part of the evidence.
- Bounded fixes can use a short regression-test result; no new discovery cycle
  or paid benchmark is required just because this contract exists.
- Readiness, demonstrated benefit, and release status remain separate decisions.

### Evaluation and learning

The immediate evaluation checks that the contract flows through the existing
skills and templates, local links and installation hashes are consistent,
and both batching and a bounded bug fix can use it without extra ceremony.
These checks establish guidance consistency, not improved engineering outcomes.

The next evaluation is to apply the guidance to subsequent work and compare
the clarity of its hypothesis, acceptance evidence, non-regression checks, and
learning record with the earlier batch artifacts. Watch for added overhead or
unsupported completion claims. Keep the guidance where it helps; simplify or
revise it where it does not.

This is reuse of the slide at the process level: domain experience improves
the skills, the skills guide the next change, and evaluations refine both the
system and the team's knowledge. There are two reinforcing loops:

- **Build and retain:** use the skills to deliver changes and preserve their
  decisions and evidence.
- **Learn and reuse:** use those records to identify value, improve the skills,
  and inform the next system or experiment.

It is not a retrospective claim that the
original implementation was defective or that this guidance has already
produced a measured productivity gain.

## Subscription-aware batch execution

### The user problem and the domain insight

Claude Code and Codex users can have time-windowed subscription allowances.
A fixed benchmark may require more work than fits into the available window.
Provider availability and benchmark size are therefore separate constraints.

The insight was to separate **the experiment** from **an execution session**.
The experiment retains its tasks, attempts, model, CLI version, and policy.
An execution session admits only as much new work as the user chooses.

This pattern is relevant to other time-based quotas, although provider-specific
quota recognition must be implemented and verified rather than assumed.

### Applying the evaluation loop

| Slide stage | Application in Wharf |
|---|---|
| **Set the target** | Complete one fixed benchmark across subscription windows without duplicating accepted trials or requiring manual result cleanup. |
| **Find your start** | Identify quota-interrupted runs and recovery friction; distinguish provider limits from model failures and infrastructure errors. |
| **Make improvement** | Add `wharf run -b B`: bound new logical-trial admissions while retaining the experiment and completed results. |
| **Evaluate and compare** | Check batch budgets, identities, retained evidence, pending counts, and read-only continuation plans; retain ordinary failed trials rather than rerunning them until they pass. |
| **Keep the improvement** | Make batching a supported workflow and monitor live runs for accounting, recovery, and quota-handling defects. |

### What changed, and what did not

- **Bounded work:** `-b` limits newly admitted logical trials, not concurrency,
  passing results, or attempts per task.
- **Continuity:** completed results are banked; subsequent invocations select
  pending slots without repeating accepted trials.
- **Reproducibility:** retained task checksums and immutable configuration
  preserve the original experiment across sessions.
- **Quota safety contract:** recognized Claude/Codex quota exhaustion stops new
  agent starts while in-flight work drains. Interrupted executions are audited
  and rejected, leaving their logical slots pending.
- **Measurement integrity:** ordinary test failures remain terminal results.
  Missing costs remain unknown; retries do not become additional trials.
- **User control:** Wharf does not predict the allowance or automatically wait
  for a reset. The operator chooses when to run another batch.

See the [batch requirements](batched-runs-prd.md) for the complete contract.

### Live evidence: Claude Opus 5.5

Inspection on November 25, 2026 covered the
[Claude Code 2.1.332 run](../wharf-swe/jobs/claude-claude-opus-5-5-default-2.1.332/all-1)
with a fixed 14-task, K=4 plan: **56 logical trials**.

| Observed measure | Result |
|---|---|
| Batch budgets | 14, 4, 10, 5, 1, 1, 1 |
| Accepted trials across seven invocations | 36; each batch stayed within its budget |
| Duplicate admissions | 0 |
| Remaining logical slots | 20 |
| Agent execution | All 36 exited successfully; no recorded agent errors |
| Lifecycle completion | All 36 completed preparation, agent, verifier, and cleanup stages |
| Benchmark outcomes | 32 passing trials, 4 failed trials |
| Read-only continuation | Correctly selected one pending slot without executing it |

The four failed trials were model-output failures: two scheduler compatibility
test failures and two generated-patch compilation failures. Retaining those
failures is evidence of honest accounting, not a batch-process defect.

The [batch ledger](../wharf-swe/jobs/claude-claude-opus-5-5-default-2.1.332/all-1/batches.json)
reconciled selected, admitted, agent-started, accepted, and pending identities.
Each invocation ended with `batch-limit`; no quota signals or rejected
executions were recorded.

### What the evidence supports

**Verified:** bounded batching, retention of completed results, no duplicate
admissions, clean execution lifecycles, and a valid next-batch plan.

**Not established by this Claude snapshot:** automatic stopping on a live quota
signal, successful execution after a quota reset, or completion of all 56 trials.
The operator deliberately stopped at 99% usage, so no quota-stop event was
expected. Earlier exhausted-quota testing is separate evidence.

### Live evidence: Codex quota rejection and recovery

The retained [Codex quota validation record](../.github/tracking/2026-11-18-codex-quota-apostrophe-changes.md)
documents an operator-initiated `-b 1` invocation under exhausted quota:

- The invocation ended `quota-paused`: one slot admitted and started, zero
  accepted trials.
- Previously banked progress remained 11 completed, 11 passing, and zero
  errors, with 45 slots pending.
- An audited recovery retained 3,566 ms of rejected agent-command time; the
  next read-only plan selected the same interrupted smoke slot.
- A real provider message had exposed an ASCII-only apostrophe parser defect.
  The fix recognized typographic punctuation and retained the dated reset
  without inventing a timezone.

This establishes a live Codex rejection/recovery path, not post-reset
completion or universal quota-message compatibility. It also shows the loop
continuing after release: live evidence found a defect, a targeted fix revised
the implementation, and the operator verified the corrected behavior.

There is no controlled before/after estimate of engineering time saved, cost
reduction, or accuracy improvement. Those benefits should not be quantified
from this observation alone.

### Leadership implication and next evaluation

This is a concrete example of **human domain expertise improving the harness**.
The limitation was not a model's reasoning capability; it was a mismatch between
the workload and the way users obtain access to that model.

The improvement turns a fixed experiment into manageable execution sessions
while preserving its evaluation integrity. Leaders should assess that
operational outcome alongside answer quality, rather than using model accuracy
as the only measure of progress.

The next evidence is continuation after reset on the same pinned plan, plus
provider-specific stop/drain checks where the existing observations leave gaps.
Success means no starts after the quota latch, no accepted interrupted trials,
no duplicated completed work, and eventual completion with unchanged scoring.

## From an evaluation need to a Copilot-capable harness

**Outcome and domain insight.** The project began with a need to evaluate HVE
and coding-agent configurations, not a desire to build another runner.
According to the project lead, Pier did not support the required Copilot CLI
workflow or scale sufficiently for the intended experiments.

**Starting point and change.** DeepSWE added a
[custom Copilot adapter](../../deep-swe/copilot-agent/copilot_agent.py) and
[Pier wrapper](../../deep-swe/scripts/run_pier.sh). Wharf then made Copilot
execution, independent preparation/execution controls, recovery, and durable
trial evidence first-class platform capabilities. The
[Wharf PRD](wharf-prd.md) records the intended boundary and scale.

**Decision and reuse.** The slide's target/start/change sequence applies to
the harness itself: adapt the evaluation system when it cannot test the
product or serve the operator. The reusable outcome is an execution and
evidence platform, not merely another model result.

**Evidence boundary.** The adapter and platform contracts are documented.
The original motivation is the project lead's account, not a universal claim
that Pier cannot support Copilot. A matched throughput comparison is still
needed to quantify the Pier-to-Wharf scaling gain.

## Systems knowledge separates execution from preparation pressure

**Outcome and domain insight.** More concurrent engineering work need not
require one CPU per agent. Long-running commands and bursty Docker/image/
workspace preparation have different resource demands.

**Change and evaluation.** Wharf separated execution concurrency N from
preparation concurrency P, releasing preparation permits before agent or
verifier commands execute. The [retrospective](RETRO.md#preparation-concurrency-is-the-stability-control)
records the operating rationale and gate telemetry. On the 16-core z01 host,
the controlled [K=2 run](../.github/tracking/2026-10-08-controlled-k2-benchmark-result.md)
used **N=32/P=12** and completed 226 logical attempts in 3:16:20, retaining
156 passes, failures, and all four whole-trial retries.

**Decision and reuse.** Systems knowledge supplied the hypothesis; occupancy,
queue, and failure evidence informed a stable operating policy. Control the
resource-intensive phase instead of applying one global CPU-based limit.

**Evidence boundary.** N=32 is twice the host's core count, but it is an
admission setting, not proof of a universal 2x throughput gain. A 56-trial
WharfSWE run configured at N=64 also cannot prove 64 simultaneous executions.
The outcome here is an evidenced operating point, not improved model accuracy.

## Docker knowledge makes prebuild warm the real execution path

**Outcome and domain insight.** Cold and warm benchmarks are different
starting conditions. A successful build command does not prove that the next
agent and verifier will reuse its work.

**Change and evaluation.** Inspection found missing CLI-version-dependent
agent caches and omitted verifier preparation. Wharf corrected the path,
bounded preparation with P, and named the operator workflow `prebuild`.
[Cache validation](../.github/tracking/2026-11-08-build-cache-verification.json)
recorded **14/14 hits** for task-environment, CLI-payload, and composed-agent
caches, plus layer reuse for all 14 verifier builds. Parent images remained
unchanged and no temporary child images remained.

**Decision and reuse.** Verify actual cache reuse, not a command's name.
The [published methodology](published-benchmarks.md#machine-preparation)
now requires warm image caches for recurring comparisons and labels deliberate
cold-start experiments separately.

**Evidence boundary.** The project lead's "night and day" observation explains
why this mattered operationally; no isolated numerical cold/warm speedup is
claimed here. Prebuild cannot precompile an unknown patch or warm every
transient application cache.

## Enterprise knowledge creates an affordable .NET canary

**Outcome and domain insight.** DeepSWE covers Go, Python, TypeScript,
JavaScript, and Rust, but not .NET or Java. Enterprise engineering needs
representative coverage and a feedback loop inexpensive enough to use often.

**Change and evaluation.** Wharf's release history supplied 14 C# tasks.
The Enterprise suite became WharfSWE and a recurring "canary in the coal mine."
The [retrospective](RETRO.md) records one checkpoint: **56 WharfSWE trials,
$15.01, about 9.5 minutes**, versus **452 DeepSWE trials, $546.71, about six
hours**. These are differently sized evaluations of different task suites.

**Decision and reuse.** Use [recurring canary runs](published-benchmarks.md#cadence-and-arms)
for early signals across CLI releases, with fixed-model reference points and
deeper evaluation where needed. Domain knowledge changed the evaluation target,
not just the execution machinery.

**Evidence boundary.** The figures demonstrate the practical size/cost of
these observations, not comparable accuracy or an isolated speedup. WharfSWE
adds .NET coverage; Java remains a coverage gap. A canary complements rather
than replaces broader evaluation.

## The canary exposes HydraFusion's long tail

**Outcome and domain insight.** Strong pass rates and reasonable averages can
conceal slow outliers. Enterprise users also care about predictability and
time to finish the last item.

**Evaluation.** The retained Copilot CLI 1.0.116
[HydraFusion](../wharf-swe/data/copilot-hydrafusion-default-1.0.116.jsonl) and
[Opus 5.5](../wharf-swe/data/copilot-claude-opus-5.5-default-1.0.116.jsonl)
arms each have 56 trials, the same selected tasks/checksums and K/N/P, and
complete agent-time coverage:

| Actual agent-command duration | HydraFusion | Opus 5.5 |
|---|---:|---:|
| Median | 262.6 s | 202.2 s |
| P90, inclusive quantile | 633.4 s | 305.1 s |
| Maximum | 1,584.1 s | 398.0 s |
| Trials over 20 minutes | 4 | 0 |

**Decision and reuse.** The canary turns a suspected user-experience issue
into a distribution and concrete slow trials. The project lead reports that
the earlier HydraFusion long-tail finding was escalated to GitHub.
This is the slide's find-issues loop extending into product-team feedback.

**Evidence boundary.** The latest distribution illustrates the signal; it is
not evidence of when the earlier escalation occurred. Neither arm records
exception categories. A latency tail is not by itself a diagnosed routing
defect, and an escalation is not a completed fix. A controlled before/after
evaluation is needed to establish any resulting improvement.

## Auto turns a disappointing result into a learning opportunity

**Outcome and domain insight.** Automatic selection should be tested against
the engineering workload, not assumed to select an effective configuration.

**Evaluation and response.** The [Auto campaign](ANALYTICS.md#auto-model-selection)
passed **209/565 trials (37.0%)**. Even removing 27 agent timeouts as a
diagnostic exercise raises the rate only to 38.8%; failures remain in the
official denominator. The project lead reports that this finding catalyzed
the GitHub meeting and subsequent HydraFusion evaluation. The retained
[meeting brief](GITHUB_COPILOT_MEETING_BRIEF.md) documents the proposed
diagnosis, access request, and joint experiments.

**Decision and reuse.** Keep the disappointing baseline and use it to ask a
better product question. Later HydraFusion 1.0.98 reached **318/452 (70.35%)**,
but different CLI versions, K depths, and other conditions prevent treating
that difference as an isolated routing improvement.

**Evidence boundary.** The brief is a draft, not meeting minutes. The
collaboration chronology is attributed to the project lead. Auto's per-trial
selected models were not retained, so the evidence cannot distinguish routing,
model capability, effort, or their interactions as the cause.

## Evaluation revises assumptions about models and harnesses

**Outcome and domain insight.** Neither "Claude is best" nor "the newest model
is best" is a sufficient selection rule. Evaluate the configuration's quality,
cost, and operating constraints together.

**Same-model harness observation.** The current retained Opus 5 exports show
[Copilot](../deep-swe/data/copilot-claude-opus-5-max-1.0.77.jsonl) passing
**322/452 (71.24%)**, versus
[Claude Code](../deep-swe/data/cc-claude-opus-5-max-2.1.220.jsonl) at
**291/452 (64.38%)**, on identical task/checksum sets. Cost per costed trial
was **$5.6623 versus $13.4885**, approximately 58% lower for Copilot, with
coverage of **446/452 versus 449/452**.

**Model-generation observation.** Sibling DeepSWE
[GPT-5.6 Luna](../../deep-swe/data/copilot-gpt-5.6-luna-max-1.0.91.jsonl) passed
**316/452 (69.91%)**, while
[GPT-6 Luna](../../deep-swe/data/copilot-gpt-6-luna-max-1.0.91.jsonl) passed
**276/452 (61.06%)**: 40 fewer passes, or 8.85 percentage points.
Both exports have the same 113 task/checksum pairs, K=4, Copilot CLI 1.0.91,
and max effort. GPT-6 was also cheaper per costed trial, approximately
$0.176 versus $0.367, with coverage of 452/452 versus 447/452.

**Decision and reuse.** Evaluation changed our domain knowledge, not just the
software. Select against the observed quality/cost frontier and user outcome;
retain strong configurations rather than automatically preferring model
prestige or recency. Reuse that lesson in future experiment design.

**Evidence boundary.** These are configuration-specific aggregate observations,
not universal rankings or statistical-significance claims. Claude Code's
actual versions span 2.1.220-2.1.222 despite the filename; historical
[subagent confounds](../../deep-swe/CONFOUNDS.md) also matter. Older narrative
Opus figures differ from the current exports; the numbers above use the
retained rows and costed denominators, not a mixture of reporting snapshots.

## What did not work: complexity must earn its place

The loop is not a mechanism for justifying every idea we implement. "Keep the
improvement" also means revising or removing a change that does not demonstrate
useful value. A rejected hypothesis is reusable knowledge, not wasted work.

### Container CPU controls: a plausible hypothesis without useful benefit

We thought `--container-cpus` would make concurrent execution more predictable
and improve resource use. The project lead's assessment of the experiments
and operating experience is that it did not show useful value. It added a
configuration and tuning complication without earning its place.

That contrasts with `-p`, which addresses preparation pressure at the relevant
resource boundary, and `-b`, which fits fixed experiments into subscription
windows. Their operational value has concrete gate, cache, admission, and
recovery evidence in the cases above; CPU controls did not show a comparable
benefit. This is not a claim that CPU limits are never useful elsewhere.

**Direction:** likely remove `--container-cpus`. It is still supported;
this document records the negative finding and intended simplification,
not an implemented removal or a quantified performance effect.

### Run manifests: reconsider a configuration surface

`--manifest` offered structured, reusable configuration, but it also introduced
another authoring, validation, and precedence surface. The likely direction
is to remove it in favor of environment variables and command-line flags.
This is a simplification judgment, not evidence that manifests caused a
measured performance failure.

**Direction:** removal is under consideration, not implemented. Simplifying
configuration input must not discard the retained effective configuration,
task checksums, run plan, or immutable resume policy. Reproducibility comes
from preserving the actual experiment, not requiring one particular input
format.

### CLI-version environment variables: a completed correction

A CLI version depends on which CLI is selected. A generic ambient version pin
can carry a Copilot version into a Claude Code or Codex invocation. The
[version-policy change](../.github/tracking/2026-11-16-cli-version-policy-review.md)
therefore retired `WHARF_CLI_VERSION` and `CLI_VERSION`, rejecting them with
migration guidance rather than silently ignoring a mismatched pin.

**Current behavior:** `--cli-version` takes precedence over manifest
`cliVersion`, then the selected provider's `latest`. That contract remains
in force while `--manifest` is supported. Exact effective version identity
remains part of the retained experiment.

## Leadership implications

The slide's loop has value beyond an individual agent:

- **Expertise chooses the experiment.** Quota windows, Docker behavior,
  systems capacity, and enterprise language needs define useful targets.
- **Evaluation improves expertise.** HVE, Auto, and model-harness comparisons
  revise assumptions about what actually helps.
- **Skills make the learning reusable.** Requirements, research, review, and
  retained evidence support both delivery and later discovery of its value.
- **Complexity must earn its place.** Preserve negative findings and retire
  low-value controls; a plausible hypothesis does not justify keeping a feature.
- **Reuse has several forms.** A platform feature, an operating policy, an
  improved skill, or an actionable product-team finding can each move the
  system forward.

The recommended practice is to retain the conditions, evidence, decision, and
learning together. Keep demonstrated improvements; revise weak assumptions;
evaluate proposed fixes before claiming their benefit. This turns the slide
from a presentation into a repeatable engineering practice.

## Extending this record

For each subsequent improvement, capture:

1. The user outcome and domain constraint.
2. The baseline evidence and targeted change.
3. What remained invariant.
4. The measured outcome, limitations, and regression checks.
5. The decision to retain the improvement and the next evidence needed.

This keeps the document focused on an evidence-backed improvement loop rather
than a feature inventory. The
[research catalog](../.github/tracking/2026-11-25-continuous-improvement-candidates-research.md)
retains ten additional candidates and publication gaps for subsequent updates.
