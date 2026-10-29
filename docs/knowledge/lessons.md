---
title: Wharf lessons learned
description: What a practitioner should know after building and operating Wharf, with links to the evidence
---

## How to read this

Each lesson states the principle, the evidence that produced it, and the
practice that follows. Lessons are grouped by theme rather than by release.

## 1. Scale up before you scale out

**Lesson:** Execution concurrency and preparation concurrency are different
resources. Limit them separately.

**Evidence:** Before preparation had its own gate, large runs failed with
Docker write errors, often close to an hour in, and lost the whole run. Cold
runs were the worst case. Wharf separates trial concurrency (`N`) from
preparation concurrency (`P`). Retained gate telemetry shows the gate filling
and queueing in every measured run, with a worst wait of 27 to 45 seconds.
Operators have run `N` at about 2.5 times the processor count, including
`N=160` on a 64-core host. The Pier comparison, where practical scale-up
stopped near `N=32`, is an operator observation.
See [Preparation concurrency is the stability control](../../RETRO.md#preparation-concurrency-is-the-stability-control)
and the [shared preparation gate research](../../.github/tracking/2026-10-08-shared-preparation-gate-research.md).

**Practice:** Size `P` for the cold case and use that value for every run. A
higher warm-run `P` saves about a minute and risks hours of lost work.

## 2. A single well-used host is an enterprise feature

**Lesson:** Customers are more likely to own a 16-to-64-core Linux VM with
Docker than a dedicated cluster. Making that host productive is the product.

**Evidence:** Wharf's scope excludes multi-host orchestration deliberately,
while post-1.0 operation validated high single-host concurrency.
See the [executive summary](../../RETRO.md#executive-summary).

**Practice:** Treat "distributed" as a cost to avoid unless the workload
requires it, not as a sign of maturity.

## 3. Retries must never change what you are measuring

**Lesson:** A logical trial is the unit of measurement. Infrastructure
retries are nested evidence, not extra trials.

**Evidence:** Verifier timeouts retry only the verifier, reusing the exact
agent patch and cost. Agent timeouts and clean no-patch exits are terminal.
Reward-zero verification is not retryable. A generic verifier cleanup failure
once triggered the old whole-trial fallback, which reran a completed agent and
then aborted. See the [verifier-only retry research](../../.github/tracking/2026-10-08-verifier-only-retry-research.md)
and [Session 23 in the session log](../session-log.md).

**Practice:** Decide the retry boundary from what failed. Never rerun an
agent to repair a verifier problem.

## 4. Release resources before writing slow evidence

**Lesson:** A slow telemetry sink can make a released slot look occupied.

**Evidence:** Session 12 found that writing retry evidence before releasing a
phase slot could hold the slot falsely and undermine work-conserving behavior.
See [Session retrospectives](../../RETRO.md#session-retrospectives).

**Practice:** Release capacity first, then record what happened.

## 5. Cancellation belongs to the waiter, not the shared work

**Lesson:** When several callers share one build, one caller's cancellation
must not cancel the build for everyone else.

**Evidence:** Session 7's derived-image coordinator keeps cancellation at the
wait boundary and removes failed operations from the in-flight map so retries
are explicit. See [Session retrospectives](../../RETRO.md#session-retrospectives).

**Practice:** Separate the lifetime of shared work from the lifetime of any
single request for it.

## 6. Accepting a setting is not the same as using it

**Lesson:** Validating and rendering configuration does not prove the runtime
consumes it.

**Evidence:** The 1.0.0 pre-tag review found that resolved credentials were
ignored in favor of a machine-specific file, and that run-level timeout
overrides did not take precedence as documented. See [Release 1.0.0](../../RETRO.md#release-100).

**Practice:** Trace every effective setting through the execution boundary
with a test.

## 7. Test the deliverable, not its description

**Lesson:** A package can pass checksum, SBOM, and metadata checks yet be
impossible to execute.

**Evidence:** The 1.1.0 Debian package contained only the .NET apphost,
without its DLL and runtime files. Executing the extracted package caught it.
See [Release 1.1.0](../../RETRO.md#release-110).

**Practice:** Qualification must run the artifact a customer receives.

## 8. Identity must describe what actually executes

**Lesson:** Version selection must describe the container that runs the
agent, not an unrelated host installation.

**Evidence:** Exact CLI versions once depended on a host CLI probe, so valid
requests could fail when the host differed from the container. See
[Container version identity must not depend on the host CLI](../../RETRO.md#container-version-identity-must-not-depend-on-the-host-cli).

**Practice:** Resolve mutable aliases such as `latest` at their authoritative
source, then validate the installed payload.

## 9. Missing is not zero, and the denominator is planned

**Lesson:** Scoring must distinguish unknown from measured-zero and must not
shrink the denominator when trials fail.

**Evidence:** PASS@1 divides passing trials by every retained logical trial,
including failed, errored, timed-out, and cancelled ones. Missing costs stay
null and are never imputed, including from batch-level bills. See
[Benchmark evidence expanded without merging benchmark universes](../../RETRO.md#benchmark-evidence-expanded-without-merging-benchmark-universes).

**Practice:** Report coverage alongside every aggregate, and round only for
presentation.

## 10. Benchmarks that can be gamed will be

**Lesson:** Evaluator quality is part of benchmark correctness.

**Evidence:** Building the 14-task enterprise suite showed that reflection and
API-shape checks could reward implementations that echoed the prompt without
working. They were replaced with executable contract tests, and every baseline
and reference patch was validated before results were exported. See
[Enterprise benchmark](../../RETRO.md#enterprise-benchmark-net-coverage-at-a-fraction-of-the-cost).

**Practice:** Keep pass-to-pass regression checks separate, and carry
evaluator revision identity into exports.

## 11. Cheap evaluation changes behavior

**Lesson:** An evaluation cheap enough to run routinely produces more useful
evidence than an expensive one run occasionally.

**Evidence:** An enterprise K=4 arm is 56 trials, cost $15.01, and took about
9.5 minutes. A comparable DeepSWE arm was 452 trials, $546.71, and about six
hours. The cheap suite made per-CLI-release HydraFusion testing routine.
See [HydraFusion CLI series](../../RETRO.md#hydrafusion-cli-series).

**Practice:** Build a fast, targeted suite for regression tracking, and keep
the broad suite for periodic calibration. Never mix their pass rates.

## 12. Failed runs are experiments, not trash

**Lesson:** The evidence from a failed run is often the most valuable evidence
you have.

**Evidence:** Failed job directories were routinely deleted, so the reasoning
behind `P=12` later had to come from the operator's memory. See
[Failed runs need durable records before cleanup](../../RETRO.md#failed-runs-need-durable-records-before-cleanup).

**Practice:** Record configuration, results, errors, telemetry, and diagnosis
in `.github/tracking/` before deleting a failed job directory.

## 13. The workflow is what makes AI output trustworthy

**Lesson:** Research, Plan, Implement, Review prevents invented interfaces,
and tracking makes every decision recoverable later.

**Evidence:** Wharf went from first commit to a validated 1.0 release in seven
calendar days with 187 tracking artifacts. Independent reviews still caught
defects that package-level tests missed, such as logger wiring and help-error
propagation in 0.1.0. See [Benefits of Sessions and RPI](../../RETRO.md#benefits-of-sessions-and-rpi).

**Practice:** Keep phase boundaries, save artifacts in the repository, and run
an independent review before every release.

## 14. Treat a design hunch as a testable risk

**Lesson:** When an AI confidently defends a design that feels wrong, do not
accept or reject it on confidence alone. State the risk and design a safe
test that could distinguish the competing approaches.

**Evidence:** The LLM recommended shared Docker volumes despite the
operator's concern. Later, many upstream task images ran as root and the
shared-volume workflow had repeated permission problems. After switching to
Docker-managed images and explicit `docker cp` transfer, the operator
reported that the permission problems disappeared and caching improved
substantially. The reason for the original hunch is unknown, and the caching
improvement is an operator observation without retained telemetry proving
its cause. See [the operator observation on infrastructure advice](../../RETRO.md#operator-observation-challenge-confident-infrastructure-advice).

**Practice:** Write down the suspected failure mode, what evidence would
confirm or disprove it, and the smallest safe comparison. Preserve the
observations even when the underlying intuition cannot yet be explained.
