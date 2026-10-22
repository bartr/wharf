---
title: Enterprise task benchmark summary
description: K=4 results for completed enterprise task model runs
---

## Results

Verified 2026-10-21 from twenty-one completed `all-1` runs. Each arm contains 14
tasks and four attempts per task.

Sorted by PASS@1 descending, then P2P errors ascending, then cost ascending.
All runs use default effort and the Copilot CLI.

| Model | Effort | CLI | CLI version | Passes | PASS@1 | P2P errors | Score | Cost/trial | Avg agent time |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| GPT-6 Astra | default | copilot-cli | 1.0.102 | 56 / 56 | 100.00% | 0 | 100.00% | $1.2351 | 254.9 s |
| GPT-6.1 Sol | default | copilot-cli | 1.0.102 | 55 / 56 | 98.21% | 1 | 97.96% | $0.2307 | 333.9 s |
| **HydraFusion** | default | copilot-cli | 1.0.98 | 54 / 56 | 96.43% | 0 | 96.43% | $0.4116 | 234.8 s |
| GPT-6 Astra | default | copilot-cli | 1.0.95 | 54 / 56 | 96.43% | 0 | 96.43% | $1.3180 | 252.5 s |
| **HydraFusion** | default | copilot-cli | 1.0.96 | 54 / 56 | 96.43% | 1 | 96.18% | $0.3197 | 272.5 s |
| GPT-6 Luna | default | copilot-cli | 1.0.95 | 54 / 56 | 96.43% | 2 | 95.93% | $0.0158 | 257.5 s |
| GPT-6.1 Sol | default | copilot-cli | 1.0.95 | 54 / 56 | 96.43% | 2 | 95.93% | $0.2200 | 283.7 s |
| **HydraFusion** | default | copilot-cli | 1.0.100 | 53 / 56 | 94.64% | 1 | 94.39% | $0.2932 | 272.0 s |
| **HydraFusion** | default | copilot-cli | 1.0.97 | 52 / 56 | 92.86% | 1 | 92.61% | $0.3416 | 316.4 s |
| **HydraFusion** | default | copilot-cli | 1.0.101 | 52 / 56 | 92.86% | 1 | 92.61% | $0.3527 | 218.9 s |
| Claude Opus 5.5 | default | copilot-cli | 1.0.91 | 52 / 56 | 92.86% | 3 | 92.11% | $0.4792 | 180.8 s |
| **HydraFusion** | default | copilot-cli | 1.0.95 | 51 / 56 | 91.07% | 0 | 91.07% | $0.3186 | 302.9 s |
| **HydraFusion** | default | copilot-cli | 1.0.102 | 51 / 56 | 91.07% | 2 | 90.57% | $0.3341 | 266.9 s |
| Gemini 3.8 Flash | default | copilot-cli | 1.0.95 | 50 / 56 | 89.29% | 3 | 88.54% | $0.8911 | 801.5 s |
| GPT-6 Luna | default | copilot-cli | 1.0.102 | 50 / 56 | 89.29% | 4 | 88.29% | $0.0152 | 258.3 s |
| Claude Opus 5.5 | default | copilot-cli | 1.0.95 | 49 / 56 | 87.50% | 5 | 86.25% | $0.5310 | 215.9 s |
| Claude Opus 5 | default | copilot-cli | 1.0.91 | 48 / 56 | 85.71% | 2 | 85.21% | $1.0990 | 230.7 s |
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

| Model | Cost coverage | Cost per costed trial | Average cost per task | Mean agent time | Mean trial time |
|---|---:|---:|---:|---:|---:|
| GPT-6 Astra 1.0.102 | 56 / 56 | $1.2351 | $4.9406 | 254.9 s | 335.1 s |
| GPT-6.1 Sol 1.0.102 | 56 / 56 | $0.2307 | $0.9229 | 333.9 s | 420.2 s |
| HydraFusion 1.0.98 | 56 / 56 | $0.4116 | $1.6464 | 234.8 s | 270.5 s |
| GPT-6 Astra 1.0.95 | 56 / 56 | $1.3180 | $5.2719 | 252.5 s | 352.5 s |
| HydraFusion 1.0.96 | 56 / 56 | $0.3197 | $1.2790 | 272.5 s | 378.6 s |
| GPT-6 Luna 1.0.95 | 56 / 56 | $0.0158 | $0.0633 | 257.5 s | 386.7 s |
| GPT-6.1 Sol 1.0.95 | 56 / 56 | $0.2200 | $0.8798 | 283.7 s | 394.6 s |
| HydraFusion 1.0.100 | 56 / 56 | $0.2932 | $1.1730 | 272.0 s | 370.9 s |
| HydraFusion 1.0.97 | 56 / 56 | $0.3416 | $1.3662 | 316.4 s | 412.3 s |
| HydraFusion 1.0.101 | 56 / 56 | $0.3527 | $1.4108 | 218.9 s | 245.0 s |
| Claude Opus 5.5 1.0.91 | 56 / 56 | $0.4792 | $1.9166 | 180.8 s | 261.9 s |
| HydraFusion 1.0.95 | 56 / 56 | $0.3186 | $1.2742 | 302.9 s | 422.2 s |
| HydraFusion 1.0.102 | 56 / 56 | $0.3341 | $1.3366 | 266.9 s | 338.2 s |
| Gemini 3.8 Flash 1.0.95 | 56 / 56 | $0.8911 | $3.5644 | 801.5 s | 877.5 s |
| GPT-6 Luna 1.0.102 | 56 / 56 | $0.0152 | $0.0607 | 258.3 s | 365.3 s |
| Claude Opus 5.5 1.0.95 | 56 / 56 | $0.5310 | $2.1242 | 215.9 s | 354.3 s |
| Claude Opus 5 1.0.91 | 56 / 56 | $1.0990 | $4.3958 | 230.7 s | 268.1 s |
| Claude Opus 5.5 1.0.102 | 56 / 56 | $0.4785 | $1.9140 | 214.5 s | 345.7 s |
| MAI Code 1.1 Flash 1.0.95 | 56 / 56 | $0.0770 | $0.3081 | 275.1 s | 334.1 s |
| GPT-5.6 Luna 1.0.95 | 56 / 56 | $0.0231 | $0.0923 | 190.6 s | 318.1 s |
| GPT-5.6 Terra 1.0.96 | 56 / 56 | $0.2463 | $0.9851 | 141.5 s | 212.8 s |

Cost uses the retained Copilot AI Credit evidence at one cent per credit.
Average cost per task includes all four attempts for each of the 14 tasks. No
costs were imputed. Agent and trial duration evidence is present for every
exported trial.

## Data coverage

* Exported arms: 21
* Exported logical trials: 1176
* Unique tasks per arm: 14
* Attempts per task: 4
* Functional and regression measurements: 1176 / 1176 trials
* Agent and trial duration measurements: 1176 / 1176 trials
* Trials with an exception category: 0

The empty Gemini `all-2` directory is excluded.
