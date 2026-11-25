# Continuous system improvement: candidate case studies

## Purpose and recommendation

Research shortlist for the TLT-facing
[continuous system improvement document](../../docs/Continuous-Agent-Improvement.md).
Reviewed Wharf documentation, retained exports, qualification records, and
commit history, plus the sibling DeepSWE repository.

Use **domain expertise -> hypothesis -> evaluation -> decision -> reusable
learning** as the organizing story. The important subject is the complete
system: model, harness, guidance, infrastructure, evaluator, and operating
practice. Wharf need not be called an agent.

Start with candidates 1-8 below. They connect the initiative's origin, systems
improvements, enterprise coverage, and product feedback. Candidates 9-18 offer
additional engineering lessons, including rejected assumptions and changes
that improved the trustworthiness of the evaluation itself.

This is research, not a claim that every candidate is ready for publication.
No benchmark, paid agent invocation, runtime change, or historical-evidence
rewrite was performed. The existing two case studies were not rewritten.

## Evidence labels

- **Measured:** recomputed from retained exports or explicitly recorded validation.
- **Documented:** repository design, implementation, or historical account.
- **Operator account:** the user's recollection; valid narrative provenance,
  but not independent measurement or a dated meeting record.
- **Gap:** evidence still needed before making a stronger claim.

## Priority candidates

### 1. From an evaluation need to a Copilot-capable, scalable harness

**Insight:** An existing benchmark runner is only useful if it supports the
product being evaluated and the required operating scale.

**Change:** DeepSWE added a custom Copilot adapter around Pier; Wharf became a
Copilot-first harness with explicit preparation/execution boundaries and
durable evidence.

**Evidence:** The sibling
[Copilot adapter](../../../deep-swe/copilot-agent/copilot_agent.py) extends
Pier's installed-agent base (lines 93, 175-219), and its
[wrapper](../../../deep-swe/scripts/run_pier.sh) invokes the local entry point
(lines 1-8). Wharf's [PRD](../../docs/wharf-prd.md) records the intended scale
and product boundary. The user supplies the origin story: Pier did not support
the desired Copilot CLI workflow and did not scale sufficiently for this work.

**Learning / slide mapping:** Set a target; discover that the starting tool
does not meet it; change the harness rather than blaming the model.

**Gap:** Do not generalize this into "Pier never supports Copilot" or a proven
head-to-head throughput advantage. Desired concurrency is not qualified
concurrency, and the original scalability comparison needs retained evidence.

### 2. Systems knowledge: separate execution concurrency from preparation pressure

**Insight:** Long-running agent commands and bursty Docker/image/workspace
preparation consume different resources. CPU count alone is a poor global
admission limit.

**Change:** Separate N from P; gate preparation without holding the permit
during agent or verifier execution.

**Evidence:** The [retrospective](../../docs/RETRO.md), lines 399-435, records
N=32/P=12 on the 16-core z01 host and preparation occupancy/queue telemetry.
The [controlled K=2 result](2026-10-08-controlled-k2-benchmark-result.md)
retains 226 attempts, 156 passes, a 3:16:20 wall time, four whole-trial retries,
and the relevant failure/cost evidence. A smaller WharfSWE arm used N=64/P=16,
but its 56 trials cannot demonstrate 64 simultaneously executing trials.

**Learning / slide mapping:** Expertise suggests a resource-specific
hypothesis; telemetry tests it; retain the stable operating policy.

**Gap:** "2x vCPU or more" describes observed admission settings, not a universal
throughput guarantee or proof that doubling N halves elapsed time.

### 3. Docker knowledge: make prebuild warm the caches that run actually uses

**Insight:** A command called "build" can finish successfully without warming
the production execution path. Cold and warm runs are different experiments.

**Change:** Resolve the exact CLI version; warm task-environment, CLI-payload,
composed-agent, and verifier build caches; bound preparation with P; rename
the workflow to `prebuild`.

**Evidence:** [Cache validation](2026-11-08-build-cache-verification.json),
lines 8-12 and 31-54, records the original omissions and subsequent 14/14
cache hits in each agent cache category, 14 verifier layer-reuse checks,
preserved ECR parent identity, and zero temporary child images left behind.
Commits `5bc68f5269fc82a767891dfffdd1a893dcc0f5a2` and
`d9c358c17d49028ae69c4b6f2f69a2f046789677` establish the fix and rename.
[Published methodology](../../docs/published-benchmarks.md), lines 24-34,
now requires warm image caches for recurring arms.

**Learning / slide mapping:** Find the wrong starting condition; change the
mechanism; verify real reuse instead of trusting the command name.

**Gap:** The user's "night and day" cold/warm observation is not an isolated
numerical speedup in this record. Prebuild does not precompile an unknown
patch or warm all transient application caches.

### 4. Enterprise domain knowledge: build a representative, inexpensive .NET canary

**Insight:** DeepSWE's Go, Python, TypeScript, JavaScript, and Rust tasks omit
.NET and Java. An enterprise evaluation strategy needs coverage beyond that
benchmark and a feedback loop cheap enough to run frequently.

**Change:** Build 14 C# tasks from Wharf's own release history; evolve the
Enterprise suite into WharfSWE; use it as a recurring release canary.

**Evidence:** The [retrospective](../../docs/RETRO.md), lines 470-524, records
the motivation and a checkpoint comparison: 56 WharfSWE trials at $15.01 and
about 9.5 minutes, versus 452 DeepSWE trials at $546.71 and about six hours.
[Published methodology](../../docs/published-benchmarks.md), lines 14-22,
defines daily HydraFusion and weekly fixed-model reference series.

**Learning / slide mapping:** Refine the target using enterprise knowledge;
choose a faster evaluation; turn isolated testing into a repeatable canary.

**Gap:** These are different suites, not comparable pass rates or an isolated
speedup. WharfSWE adds .NET coverage; it does not close the Java gap.

### 5. The canary exposes HydraFusion's duration tail, not just its average

**Insight:** A high pass rate can conceal a long latency tail. Operators care
about slow outliers as well as mean time and cost.

**Measured example:** In the current WharfSWE Copilot 1.0.116 exports,
HydraFusion has median agent time **262.6 s**, inclusive P90 **633.4 s**,
and maximum **1,584.098 s**. Eight of 56 trials exceed ten minutes; four
exceed twenty. Opus 5.5 on the same CLI has median **202.2 s**, P90 **305.1 s**,
maximum **397.991 s**, and no trials over ten minutes.

**Evidence:** [HydraFusion export](../../wharf-swe/data/copilot-hydrafusion-default-1.0.116.jsonl)
and [Opus export](../../wharf-swe/data/copilot-claude-opus-5.5-default-1.0.116.jsonl),
56 rows each. Their retained run plans have identical selected tasks/checksums
and K/N/P; agent-duration coverage is complete, and neither export contains
exception categories. Durations are actual agent-command time, not total run
wall time. P90 uses Python's inclusive quantile method.

**Learning / slide mapping:** A representative canary finds an issue; give the
product team an actionable distribution and concrete slow trials.

**Gap:** This measures a tail, not its root cause, a routing defect, or a
completed improvement. The earlier discovery and GitHub escalation are the
user's account; a dated escalation and a controlled before/after evaluation
would complete this case.

### 6. Auto: turn a disappointing result into a product-learning opportunity

**Insight:** Automatic selection should be evaluated, not assumed to select
the best configuration for repository-level engineering.

**Evidence:** [Analytics](../../docs/ANALYTICS.md), lines 568-615, records
Auto at **209/565 (37.0%)**. Excluding its 27 agent timeouts only raises the
diagnostic rate to 38.8%; do not use that exclusion as the official score.
Later HydraFusion 1.0.98 retained **318/452 (70.35%)**, but this is not an
identically controlled Auto-to-HydraFusion experiment.
The [meeting brief](../../docs/GITHUB_COPILOT_MEETING_BRIEF.md), lines 94-106
and 136-184, proposes diagnosis, preview access, and joint evaluation.

**Learning / slide mapping:** Find the issue; challenge the default; reuse the
evaluation as product feedback rather than merely publishing a leaderboard.

**Gap:** The user reports that Auto catalyzed the meeting and HydraFusion
evaluation. The draft establishes the proposed agenda, not that causal
chronology. Per-trial routing telemetry is absent, so do not diagnose routing
as the proven cause.

### 7. HVE: more guidance is a hypothesis, not an automatic improvement

**Insight:** Guidance should justify its quality, context, latency, and cost
effects. Evaluate the integration people would actually use.

**Evidence:** In sibling DeepSWE commit
`4b9c0f829b1107898797b44cde0632548eb3a9ce`, historical `hve-summary.md`
lines 11-15 report an eight-task Go panel at **23/32 passes for both baseline
and HVE**. Lines 27 and 35 distinguish a small always-on note plus invokable
skills from the discarded approximately **22K-token** force-injected
experiment. Lines 204-210 explain the need for objective code-quality evidence.
Retrieve that historical document with
`git -C ../deep-swe show 4b9c0f829b1107898797b44cde0632548eb3a9ce:hve-summary.md`.

The current Wharf-side retained 1.0.77 exports also provide larger observations:

| Configuration | Baseline passes | HVE passes | Trials per arm |
| --- | ---: | ---: | ---: |
| Opus 5 | 322 | 300 | 452 |
| GPT-5.6 Luna | 335 | 309 | 452 |

Sources: [Opus baseline](../../deep-swe/data/copilot-claude-opus-5-max-1.0.77.jsonl),
[Opus HVE](../../deep-swe/data/copilot-claude-opus-5-max-hve-1.0.77.jsonl),
[Luna baseline](../../deep-swe/data/copilot-gpt-5.6-luna-max-1.0.77.jsonl), and
[Luna HVE](../../deep-swe/data/copilot-gpt-5.6-luna-max-hve-1.0.77.jsonl).
Each baseline/HVE pair has the same task/checksum set. These observations
do not demonstrate that additional guidance improved quality.

**Learning / slide mapping:** Compare against the starting point; keep useful
guidance and revise or remove unnecessary context. The domain knowledge about
guidance changes as a result of the evaluation.

**Gap:** Account for historical CLI/subagent confounds and integration
differences before making a causal claim. The current Terra arms have
different K depths and should not be presented as the same design. Working
with the HVE team on "shrinking" HVE is the user's account and ongoing work,
not a measured completed outcome.

### 8. Evaluate model + harness + skills, not prestige or recency

**Insight:** "Claude is best" or "the newest model is best" is not a sufficient
product-selection rule. Measure the complete configuration's quality/cost frontier.

**Measured examples:**

- Current Wharf-side Opus 5 exports: Copilot **322/452 (71.24%)**, Claude
  Code **291/452 (64.38%)**. Cost per costed trial is **$5.6623** versus
  **$13.4885**, approximately 58% lower for Copilot. Cost coverage is
  **446/452** versus **449/452**. Task/checksum sets are identical.
- Sibling DeepSWE Luna 1.0.91 exports: GPT-5.6 Luna **316/452 (69.91%)**,
  GPT-6 Luna **276/452 (61.06%)**: **40 fewer passes / 8.85 percentage points**.
  Both have 113 identical task/checksum pairs, K=4, Copilot CLI 1.0.91,
  and max effort. GPT-6 is also cheaper per costed trial, approximately
  **$0.176** versus **$0.367**; cost coverage is 452/452 versus 447/452.

Sources: [Copilot Opus](../../deep-swe/data/copilot-claude-opus-5-max-1.0.77.jsonl),
[Claude Code Opus](../../deep-swe/data/cc-claude-opus-5-max-2.1.220.jsonl),
[sibling Luna 5.6](../../../deep-swe/data/copilot-gpt-5.6-luna-max-1.0.91.jsonl),
and [sibling Luna 6](../../../deep-swe/data/copilot-gpt-6-luna-max-1.0.91.jsonl).

**Learning / slide mapping:** Evaluation revises domain assumptions, and the
new knowledge becomes the next configuration's starting point.

**Gap:** These are configuration-specific observations, not universal rankings
or statistical significance claims. Claude Code's actual versions span
2.1.220/2.1.221/2.1.222 despite the filename. Historical subagent/model mixing
and other confounds remain relevant. Some narrative documents quote older
Opus counts/costs; reconcile them against the retained exports before writing
a final case study rather than silently mixing the two sets of figures.

## Additional candidates worth keeping

| Candidate | Domain insight -> change or learning | Evidence and publication boundary |
| --- | --- | --- |
| **9. Tool/subagent economics** | More delegation may buy a small quality gain at substantial additional cost; measure the marginal trade-off. | Sibling [analytics](../../../deep-swe/ANALYTICS.md), lines 794-817: a historical matched 40-task Sol panel fell from 128/160 to 124/160 passes without the built-in `task` tool, while cost fell 41.1%. Limited pilot, not a universal recommendation to disable delegation. |
| **10. Agent-free baselines** | Before attributing failure or slowness to a model, validate task infrastructure and the verifier independently. | [Wharf baseline contract](../../docs/wharf.md), lines 60-107, and commit `d708db321789d8721407732bbb8d67218ee97383`: no-op/oracle modes, trusted reference isolation, separate analytics. Implemented diagnostic mechanism; quantify any resulting savings separately. |
| **11. Evaluator quality is part of system quality** | Tests that reward identifiers or prompt mimicry can reward the wrong implementation; use executable behavior and regression protection. | [Task quality review](2026-10-11-enterprise-task-quality-review.md) and [retrospective](../../docs/RETRO.md), evaluator-quality section. The record reports 43 feature and 14 compatibility tests passing. Revised evaluators require a new controlled comparison; do not relabel historical scores. |
| **12. pnpm recursion was a harness-layout defect** | A retained HOME inside the project can make package-manager staging recursively rediscover project policy. | [Koota root-cause research](2026-10-08-koota-pnpm-recursion-research.md), runtime-layout findings: project settings defeated an environment-only mitigation. Moving HOME outside the project addressed the mechanism without weakening non-root isolation or rewriting the task. The earlier [resource-guard review](2026-10-08-agent-resource-guards-review.md) had declared an environment-based fix sufficient: this is a valuable example of revising an initially plausible conclusion. |
| **13. Container isolation and explicit ownership** | Docker filesystem/ownership knowledge can remove host-mount problems while keeping cleanup safe. | [Retrospective](../../docs/RETRO.md), lines 230-246, records the move to mountless create/start/exec/copy/remove workflows and resolution of permission issues. Treat the caching benefit there as an operator observation, not an isolated measured gain; never generalize cleanup to unrelated resources. |
| **14. Retry the failed boundary, not expensive successful work** | A verifier failure does not justify rerunning an already-completed agent and charging for a different patch. | [Controlled decision](2026-10-08-controlled-k2-benchmark-decision.md) and [result](2026-10-08-controlled-k2-benchmark-result.md) retain the earlier whole-trial retry behavior and explicitly identify the later verifier-only fix. Good case for immutable patches, distinct retry evidence, and honest trial accounting; savings need a controlled measurement. |
| **15. Effective configuration beats labels** | Model names and requested CLI versions can conceal actual CLI drift and off-model delegation. | Sibling [confounds](../../../deep-swe/CONFOUNDS.md), lines 1-95, and [retrospective](../../docs/RETRO.md), lines 300-370. Record effective versions/model telemetry and immutable resume policy. Do not claim these confounds explain a particular score difference without tracing that result. |
| **16. Validate the packaged product, not a proxy** | Metadata/checksum/SBOM success does not prove the installed command runs. | [Release qualification](../../docs/release-qualification.md), lines 3-35: executing the extracted package found missing `wharf.dll`/exit 154; full publish output plus an executable qualification gate fixed it. Failed package evidence was retained. |
| **17. Cloud preflight must inspect the resource actually used** | On Azure, Docker's data disk may be healthy even when the root volume is small. | [Storage preflight record](2026-11-24-docker-storage-preflight.md): inspect DockerRootDir, not `/`; 58 targeted tests plus an actual non-root filesystem check, without lowering the 50 GiB threshold. A precise example of deployment knowledge correcting an invalid assumption. |
| **18. Preserve failure evidence and freeze the scoring contract** | Lost failure logs and post-result exclusions undermine learning; missing costs and retries must not become success-shaped measurements. | [Retrospective](../../docs/RETRO.md), lines 435-468, and the pre-run [controlled decision](2026-10-08-controlled-k2-benchmark-decision.md). Sibling [analytics](../../../deep-swe/ANALYTICS.md), lines 480-489 and 746-784, also documents incomplete-cost denominators and final cumulative credit accounting. Count logical trials, retain failures, keep costs null when unknown, and distinguish task accuracy from PASS@1. |

## Evidence that strengthens the existing batch case

This is an extension of Case 1, not a new independent case study.

The [Codex quota record](2026-11-18-codex-quota-apostrophe-changes.md),
especially its operator-initiated live-validation section, contains the
earlier exhausted-quota evidence that the current document says should be
attached:

- A typographic apostrophe in `You've` caused the original ASCII-only parser
  to misclassify a real provider quota rejection and rerun the whole trial.
- The fix normalized that punctuation and preserved a dated, timezone-free
  reset without inventing a timezone.
- The operator's exhausted-quota `-b 1` invocation ended `quota-paused`,
  admitted/started one slot, accepted zero trials, and retained 3,566 ms of
  rejected agent-command time in an audited recovery record.
- Previously banked progress remained 11 completed/11 passing/zero errors;
  45 slots were pending, and the next read-only plan selected the same
  interrupted smoke slot.

This proves a real quota-rejection/recovery path for that Codex configuration.
Keep it distinct from the later Claude run deliberately stopped at 99% usage,
which verified bounded batching and preserved progress but did not reach a
quota rejection. Do not generalize provider formatting/timezone behavior.

## Publication gaps and recommended next evidence

1. Attach dated operator/product-team evidence for the Auto -> GitHub meeting
   -> HydraFusion-access sequence and the long-tail escalation. Repository
   drafts and exports alone do not establish those events.
2. Record the HVE team's agreed reduction hypothesis and acceptance criteria;
   do not present ongoing collaboration as a completed quality improvement.
3. Recover a matched cold/warm comparison before publishing a numerical
   prebuild speedup or claiming a quantified Pier-to-Wharf scaling gain.
4. Reconcile historical prose with current canonical exports, especially
   Opus counts/costs and HydraFusion's retained versus planned denominator.
   Preserve original evidence and disclose changes in evaluation contract.
5. For a product fix, show a controlled before/after evaluation. A discovered
   issue and an escalation are useful outcomes, but not proof it was fixed.

## Validation

Recomputed row counts, pass totals, costed denominators, and task/checksum-set
identity for the cited Wharf-side Opus/HVE exports. Recomputed complete
agent-duration distributions and exact ten-/twenty-minute thresholds for
the current WharfSWE HydraFusion/Opus arms; checked their run-plan task
selection and K/N/P agreement. Sibling DeepSWE research verified Luna's
actual row metadata, K depth, checksum identity, costs, and exception counts,
and identified the historical HVE report by full commit and line references.
Documentation-only research requires no runtime test or benchmark execution.
