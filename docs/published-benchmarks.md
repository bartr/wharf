---
title: Published benchmark methodology
description: How recurring Wharf enterprise benchmark results are produced, charted, and audited for public display
---

# Published benchmark methodology

Frontier Engineering uses Wharf to run recurring enterprise benchmarks and
plans to publish the results as charts, most likely on fe-labs.ai. This page
defines what each chart measures, how points are produced, and what evidence
backs them, so the published series can be audited and explained.

## Cadence and arms

| Series | Cadence | Purpose |
|---|---|---|
| HydraFusion | Daily | Track routing quality, regressions, cost, and time across Copilot CLI releases |
| Claude Opus and OpenAI models | Weekly | Fixed-model reference points for comparison with HydraFusion |

Each point is one completed enterprise arm: 14 C# tasks with K=4 attempts,
for 56 logical trials. Runs use the Copilot CLI with default effort unless a
chart says otherwise.

## Chart views

The site presents three separate views of the same retained trials. Each
view answers a different question, and none is a general model ranking.

### Score

Score is the trial pass rate with a regression penalty:

```text
score = passes / trials - (p2p_errors * 0.0025)
```

* `passes` counts logical trials with `reward > 0`.
* `trials` is every retained logical trial in the planned denominator,
  including failed, errored, timed-out, and cancelled trials.
* `p2p_errors` counts trials whose patch failed at least one pre-existing
  pass-to-pass test.

Each P2P error subtracts 0.25 percentage points. Breaking working code is
more expensive than failing to complete a task, so a regression lowers the
score even when the trial is otherwise counted. Score is displayed as a
percentage, is not clamped at zero, and is null for an empty arm. It is
deliberately separate from PASS@1; the PASS@1 and P2P count should be
available on hover or in the data table.

The penalty is small relative to one trial: a single pass or fail moves an
arm by about 1.79 percentage points (1/56), while one P2P error moves it by
0.25. The score is designed to break ties and expose regression-prone
configurations, not to dominate pass rate.

### Agent time

Agent time is the mean actual agent-command duration per logical trial, in
seconds. It sums agent durations across whole-trial retries and excludes
image preparation, workspace preparation, evidence copying, and verifier
time. It reflects how long the model and harness work on a task, not host
throughput or wall-clock run duration.

### Cost

Cost is the mean cost per costed logical trial in USD. Copilot AI Credits
convert at one cent per credit. Trials without cost evidence stay null and
are excluded from the cost mean; they are never imputed or replaced with
zero. Every cost point must show cost coverage, for example `56 / 56`.

## Statistical presentation

A 56-trial arm is a small sample. A one- or two-trial change between days is
usually noise, not a regression or an improvement.

* Show a 95% Wilson interval for the pass rate behind each score point. For
  example, 52/56 passes is 92.9% with an interval of about 83.0%–97.2%, and
  54/56 is 96.4% with an interval of about 87.9%–99.0%.
* Show a rolling mean (for example, seven days for the daily series) next to
  the individual daily points.
* Do not describe a change as significant unless the intervals or a stated
  statistical test support it.
* Show the trial count for every point.

## Comparability

A point compares cleanly with another only when these match:

* Wharf version
* Enterprise task revision and task checksums
* K depth and planned trial count
* Host class and preparation settings
* Copilot CLI version, or an explicit annotation that the CLI changed

Mark every change to these on the charts as an annotation, so viewers do not
mistake an infrastructure or harness change for a model change. Daily
HydraFusion points and weekly fixed-model points should share the same
configuration whenever they appear on the same chart.

## Scope and labeling

* Label every chart as the 14-task C# enterprise benchmark.
* Do not combine enterprise results with the 113-task DeepSWE benchmark.
  Publish them as separate series, and never average their pass rates.
* State that the results measure these tasks under this harness, not overall
  model capability.

## Evidence and auditability

Every published point must trace to retained evidence:

1. Export the completed run with `./wharf analytics export` to one JSONL row
   per logical trial. Infrastructure and verifier retries remain nested
   evidence and never become additional trials.
2. Rebuild the manifest with
   `./wharf analytics manifest --data-directory enterprise/data`.
3. Validate row count, unique logical-trial identity, task coverage, K depth,
   reward totals, P2P counts, cost conversion, and duration coverage before
   publishing.
4. Publish the exported JSONL and manifest entry, or at least their
   checksums, with each point, together with a link to this methodology.

Failed, incomplete, or excluded runs must still be recorded, for example in
`.github/tracking/`, with the reason and evidence. Publishing only successful
runs would bias the series. Any exclusion must be decided by a stated rule,
not after viewing the result.

## Approval before publication

HydraFusion is a pre-release product. Confirm that public daily results are
approved by the appropriate Microsoft owners before launch. Also confirm that
the terms governing the Anthropic and OpenAI models permit publishing
benchmark results.

## Related documentation

* [README analytics rules](../README.md#1-canonical-analytics-rules)
* [Enterprise benchmark summary](../enterprise/data/summary.md)
* [Enterprise benchmark in the retrospective](../RETRO.md#enterprise-benchmark-net-coverage-at-a-fraction-of-the-cost)
* [HydraFusion CLI series](../RETRO.md#hydrafusion-cli-series)
