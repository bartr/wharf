---
title: Enterprise task benchmark summary
description: K=4 results for completed enterprise task model runs
---

## Results

Verified 2026-10-12 from ten completed `all-1` runs. Each arm contains 14
tasks and four attempts per task.

| Model | Passes | PASS@1 | P2P errors | Cost/run |
|---|---:|---:|---:|---:|
| GPT-6 Astra | 54 / 56 | 96.43% | 0 | $1.3180 |
| GPT-6 Luna | 54 / 56 | 96.43% | 2 | $0.0158 |
| GPT-6.1 Sol | 54 / 56 | 96.43% | 2 | $0.2200 |
| Claude Opus 5.5 | 52 / 56 | 92.86% | 3 | $0.4792 |
| HydraFusion | 51 / 56 | 91.07% | 0 | $0.3186 |
| Gemini 3.8 Flash | 50 / 56 | 89.29% | 3 | $0.8911 |
| Claude Opus 5 | 48 / 56 | 85.71% | 2 | $1.0990 |
| MAI Code 1.1 Flash | 46 / 56 | 82.14% | 5 | $0.0770 |
| GPT-5.6 Luna | 46 / 56 | 82.14% | 6 | $0.0231 |
| GPT-5.6 Terra | 44 / 56 | 78.57% | 2 | $0.2463 |

PASS@1 is the trial-level pass rate. P2P errors count trial patches that failed
at least one pre-existing test. Cost/run divides known cost by cost-bearing
logical trials; all rows in this table have 56/56 cost coverage.

## Cost and timing

| Model | Cost coverage | Cost per costed trial | Average cost per task | Mean agent time | Mean trial time |
|---|---:|---:|---:|---:|---:|
| GPT-6 Luna | 56 / 56 | $0.0158 | $0.0633 | 257.5 s | 386.7 s |
| GPT-6.1 Sol | 56 / 56 | $0.2200 | $0.8798 | 283.7 s | 394.6 s |
| GPT-6 Astra | 56 / 56 | $1.3180 | $5.2719 | 252.5 s | 352.5 s |
| HydraFusion | 56 / 56 | $0.3186 | $1.2742 | 302.9 s | 422.2 s |
| Gemini 3.8 Flash | 56 / 56 | $0.8911 | $3.5644 | 801.5 s | 877.5 s |
| Claude Opus 5.5 | 56 / 56 | $0.4792 | $1.9166 | 180.8 s | 261.9 s |
| Claude Opus 5 | 56 / 56 | $1.0990 | $4.3958 | 230.7 s | 268.1 s |
| GPT-5.6 Luna | 56 / 56 | $0.0231 | $0.0923 | 190.6 s | 318.1 s |
| GPT-5.6 Terra | 56 / 56 | $0.2463 | $0.9851 | 141.5 s | 212.8 s |
| MAI Code 1.1 Flash | 56 / 56 | $0.0770 | $0.3081 | 275.1 s | 334.1 s |

Cost uses the retained Copilot AI Credit evidence at one cent per credit.
Average cost per task includes all four attempts for each of the 14 tasks. No
costs were imputed. Agent and trial duration evidence is present for every
exported trial.

## Data coverage

* Exported arms: 10
* Exported logical trials: 560
* Unique tasks per arm: 14
* Attempts per task: 4
* Functional and regression measurements: 560 / 560 trials
* Agent and trial duration measurements: 560 / 560 trials
* Trials with an exception category: 0

The empty Gemini `all-2` directory is excluded.
