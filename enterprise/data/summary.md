---
title: Enterprise task benchmark summary
description: K=4 results for completed enterprise task model runs
---

## Results

Verified 2026-11-09 from thirty completed runs. Each arm contains 14
tasks and four attempts per task.

Sorted by PASS@1 descending, then P2P errors ascending, then cost ascending.
All runs use default effort and the Copilot CLI.

| Model | Effort | CLI | CLI version | Passes | PASS@1 | P2P errors | Score | Cost/trial | Avg agent time |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| GPT-6 Astra | default | copilot-cli | 1.0.102 | 56 / 56 | 100.00% | 0 | 100.00% | $1.2351 | 254.9 s |
| GPT-6.1 Sol | default | copilot-cli | 1.0.105 | 55 / 56 | 98.21% | 0 | 98.21% | $0.2298 | 224.8 s |
| GPT-6.1 Sol | default | copilot-cli | 1.0.102 | 55 / 56 | 98.21% | 1 | 97.96% | $0.2307 | 333.9 s |
| **HydraFusion** | default | copilot-cli | 1.0.107 | 54 / 56 | 96.43% | 0 | 96.43% | $0.2661 | 331.5 s |
| **HydraFusion** | default | copilot-cli | 1.0.98 | 54 / 56 | 96.43% | 0 | 96.43% | $0.4116 | 234.8 s |
| GPT-6 Astra | default | copilot-cli | 1.0.105 | 54 / 56 | 96.43% | 0 | 96.43% | $1.3178 | 174.0 s |
| GPT-6 Astra | default | copilot-cli | 1.0.95 | 54 / 56 | 96.43% | 0 | 96.43% | $1.3180 | 252.5 s |
| **HydraFusion** | default | copilot-cli | 1.0.96 | 54 / 56 | 96.43% | 1 | 96.18% | $0.3197 | 272.5 s |
| GPT-6 Luna | default | copilot-cli | 1.0.95 | 54 / 56 | 96.43% | 2 | 95.93% | $0.0158 | 257.5 s |
| GPT-6.1 Sol | default | copilot-cli | 1.0.95 | 54 / 56 | 96.43% | 2 | 95.93% | $0.2200 | 283.7 s |
| **HydraFusion** | default | copilot-cli | 1.0.100 | 53 / 56 | 94.64% | 1 | 94.39% | $0.2932 | 272.0 s |
| **HydraFusion** | default | copilot-cli | 1.0.97 | 52 / 56 | 92.86% | 1 | 92.61% | $0.3416 | 316.4 s |
| **HydraFusion** | default | copilot-cli | 1.0.101 | 52 / 56 | 92.86% | 1 | 92.61% | $0.3527 | 218.9 s |
| **HydraFusion** | default | copilot-cli | 1.0.105 | 52 / 56 | 92.86% | 2 | 92.36% | $0.2681 | 188.7 s |
| Claude Opus 5.5 | default | copilot-cli | 1.0.91 | 52 / 56 | 92.86% | 3 | 92.11% | $0.4792 | 180.8 s |
| Claude Opus 5.5 | default | copilot-cli | 1.0.105 | 52 / 56 | 92.86% | 3 | 92.11% | $0.5319 | 147.2 s |
| **HydraFusion** | default | copilot-cli | 1.0.106 | 51 / 56 | 91.07% | 0 | 91.07% | $0.2895 | 378.2 s |
| **HydraFusion** | default | copilot-cli | 1.0.95 | 51 / 56 | 91.07% | 0 | 91.07% | $0.3186 | 302.9 s |
| **HydraFusion** | default | copilot-cli | 1.0.102 | 51 / 56 | 91.07% | 2 | 90.57% | $0.3341 | 266.9 s |
| **HydraFusion** | default | copilot-cli | 1.0.103 | 51 / 56 | 91.07% | 2 | 90.57% | $0.3930 | 229.4 s |
| **HydraFusion** | default | copilot-cli | 1.0.104 | 51 / 56 | 91.07% | 3 | 90.32% | $0.4028 | 196.7 s |
| Gemini 3.8 Flash | default | copilot-cli | 1.0.95 | 50 / 56 | 89.29% | 3 | 88.54% | $0.8911 | 801.5 s |
| GPT-6 Luna | default | copilot-cli | 1.0.102 | 50 / 56 | 89.29% | 4 | 88.29% | $0.0152 | 258.3 s |
| Claude Opus 5.5 | default | copilot-cli | 1.0.95 | 49 / 56 | 87.50% | 5 | 86.25% | $0.5310 | 215.9 s |
| Claude Opus 5 | default | copilot-cli | 1.0.91 | 48 / 56 | 85.71% | 2 | 85.21% | $1.0990 | 230.7 s |
| GPT-6 Luna | default | copilot-cli | 1.0.105 | 48 / 56 | 85.71% | 4 | 84.71% | $0.0156 | 197.1 s |
| Claude Opus 5.5 | default | copilot-cli | 1.0.102 | 48 / 56 | 85.71% | 4 | 84.71% | $0.4785 | 214.5 s |
| MAI Code 1.1 Flash | default | copilot-cli | 1.0.95 | 46 / 56 | 82.14% | 5 | 80.89% | $0.0770 | 275.1 s |
| GPT-5.6 Luna | default | copilot-cli | 1.0.95 | 46 / 56 | 82.14% | 6 | 80.64% | $0.0231 | 190.6 s |
| GPT-5.6 Terra | default | copilot-cli | 1.0.96 | 44 / 56 | 78.57% | 2 | 78.07% | $0.2463 | 141.5 s |

PASS@1 is the trial-level pass rate. P2P errors count trial patches that failed
at least one pre-existing test. Score is `passes / trials - (p2p_errors * 0.0025)`,
displayed as a percentage: each P2P error subtracts 0.25 percentage points.
It is separate from PASS@1 and the Enterprise Score audit metric, is not
clamped at zero, and is null for empty arms.

Average cost divides known cost by cost-bearing
logical trials; all rows in this table have 56/56 cost coverage. Average agent
time uses actual agent-command durations, summed across whole-trial retries,
and excludes preparation, evidence-copy, and verifier time. Equal K makes this
the task-weighted, K-normalized mean as well.

## Cost and timing

| Model | Cost coverage | Cost per costed trial | Mean agent time |
|---|---:|---:|---:|
| GPT-6 Astra 1.0.102 | 56 / 56 | $1.2351 | 254.9 s |
| GPT-6.1 Sol 1.0.105 | 56 / 56 | $0.2298 | 224.8 s |
| GPT-6.1 Sol 1.0.102 | 56 / 56 | $0.2307 | 333.9 s |
| HydraFusion 1.0.107 | 56 / 56 | $0.2661 | 331.5 s |
| HydraFusion 1.0.98 | 56 / 56 | $0.4116 | 234.8 s |
| GPT-6 Astra 1.0.105 | 56 / 56 | $1.3178 | 174.0 s |
| GPT-6 Astra 1.0.95 | 56 / 56 | $1.3180 | 252.5 s |
| HydraFusion 1.0.96 | 56 / 56 | $0.3197 | 272.5 s |
| GPT-6 Luna 1.0.95 | 56 / 56 | $0.0158 | 257.5 s |
| GPT-6.1 Sol 1.0.95 | 56 / 56 | $0.2200 | 283.7 s |
| HydraFusion 1.0.100 | 56 / 56 | $0.2932 | 272.0 s |
| HydraFusion 1.0.97 | 56 / 56 | $0.3416 | 316.4 s |
| HydraFusion 1.0.101 | 56 / 56 | $0.3527 | 218.9 s |
| HydraFusion 1.0.105 | 56 / 56 | $0.2681 | 188.7 s |
| Claude Opus 5.5 1.0.91 | 56 / 56 | $0.4792 | 180.8 s |
| Claude Opus 5.5 1.0.105 | 56 / 56 | $0.5319 | 147.2 s |
| HydraFusion 1.0.106 | 56 / 56 | $0.2895 | 378.2 s |
| HydraFusion 1.0.95 | 56 / 56 | $0.3186 | 302.9 s |
| HydraFusion 1.0.102 | 56 / 56 | $0.3341 | 266.9 s |
| HydraFusion 1.0.103 | 56 / 56 | $0.3930 | 229.4 s |
| HydraFusion 1.0.104 | 56 / 56 | $0.4028 | 196.7 s |
| Gemini 3.8 Flash 1.0.95 | 56 / 56 | $0.8911 | 801.5 s |
| GPT-6 Luna 1.0.102 | 56 / 56 | $0.0152 | 258.3 s |
| Claude Opus 5.5 1.0.95 | 56 / 56 | $0.5310 | 215.9 s |
| Claude Opus 5 1.0.91 | 56 / 56 | $1.0990 | 230.7 s |
| GPT-6 Luna 1.0.105 | 56 / 56 | $0.0156 | 197.1 s |
| Claude Opus 5.5 1.0.102 | 56 / 56 | $0.4785 | 214.5 s |
| MAI Code 1.1 Flash 1.0.95 | 56 / 56 | $0.0770 | 275.1 s |
| GPT-5.6 Luna 1.0.95 | 56 / 56 | $0.0231 | 190.6 s |
| GPT-5.6 Terra 1.0.96 | 56 / 56 | $0.2463 | 141.5 s |

Cost uses the retained Copilot AI Credit evidence at one cent per credit.
No costs were imputed. Agent and trial duration evidence is present for every
exported trial.

## Latest CLI 1.0.107 run

Source: [`copilot-hydrafusion-default-1.0.107/all-1`](../jobs/copilot-hydrafusion-default-1.0.107/all-1).
This completed arm used K=4, N=32, P=20, and unrestricted container CPUs.
All 56 logical trials are retained, including the two reward-zero results:
`wharf-operations` attempt 4 and `wharf-scheduler` attempt 2. Both failed
functional tests without regressing any pre-existing tests.

* PASS@1: **54 / 56 = 96.43%**
* Task accuracy: **14 / 14 = 100%**, counting a task solved on any retained attempt
* P2P errors: **0**
* Known cost: **$14.9017** across 56 / 56 trials; **$0.2661 per costed trial**
* Mean actual agent-command time: **331.5 s**
* Mean full trial, from preparation start through final cleanup: **358.2 s**
* Whole-trial retries, verifier retries, and exception categories: **0**

These timings describe this retained run, not a controlled CLI-version or
concurrency comparison. Preparation concurrency differs from earlier runs;
no arm-level cold/warm cache classification is inferred from the score.

## Data coverage

* Exported arms: 30
* Exported logical trials: 1680
* Unique tasks per arm: 14
* Attempts per task: 4
* Functional and regression measurements: 1680 / 1680 trials
* Agent and trial duration measurements: 1680 / 1680 trials
* Trials with an exception category: 0

Only completed `all-1` runs are included.
Infrastructure and verifier retries are not additional logical trials.
