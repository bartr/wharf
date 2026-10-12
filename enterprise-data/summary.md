---
title: Enterprise task benchmark summary
description: K=4 results for completed enterprise task model runs
---

## Results

Generated 2026-10-11 from seven completed `all-1` runs. Each arm contains 14
tasks and four attempts per task.

| Model | Passes | PASS@1 | Tasks solved | P2P errors |
|---|---:|---:|---:|---:|
| GPT-6 Luna | 54 / 56 | 96.43% | 14 / 14 | 2 |
| GPT-6.1 Sol | 54 / 56 | 96.43% | 14 / 14 | 2 |
| GPT-6 Astra | 54 / 56 | 96.43% | 14 / 14 | 0 |
| HydraFusion | 51 / 56 | 91.07% | 14 / 14 | 0 |
| Gemini 3.8 Flash | 50 / 56 | 89.29% | 14 / 14 | 3 |
| GPT-5.6 Luna | 46 / 56 | 82.14% | 14 / 14 | 6 |
| MAI Code 1.1 Flash | 46 / 56 | 82.14% | 13 / 14 | 5 |

PASS@1 is the trial-level pass rate. Tasks solved counts a task when any of its
four retained attempts has positive reward, so it is reported separately.
P2P errors count trial patches that failed at least one pre-existing test.

## Cost and timing

| Model | Cost coverage | Cost per costed trial | Average cost per task | Mean agent time | Mean trial time |
|---|---:|---:|---:|---:|---:|
| GPT-6 Luna | 56 / 56 | $0.0158 | $0.0633 | 257.5 s | 386.7 s |
| GPT-6.1 Sol | 56 / 56 | $0.2200 | $0.8798 | 283.7 s | 394.6 s |
| GPT-6 Astra | 56 / 56 | $1.3180 | $5.2719 | 252.5 s | 352.5 s |
| HydraFusion | 56 / 56 | $0.3186 | $1.2742 | 302.9 s | 422.2 s |
| Gemini 3.8 Flash | 56 / 56 | $0.8911 | $3.5644 | 801.5 s | 877.5 s |
| GPT-5.6 Luna | 56 / 56 | $0.0231 | $0.0923 | 190.6 s | 318.1 s |
| MAI Code 1.1 Flash | 56 / 56 | $0.0770 | $0.3081 | 275.1 s | 334.1 s |

Cost uses the retained Copilot AI Credit evidence at one cent per credit.
Average cost per task includes all four attempts for each of the 14 tasks. No
costs were imputed. Agent and trial duration evidence is present for every
exported trial.

## Data coverage

* Exported arms: 7
* Exported logical trials: 392
* Unique tasks per arm: 14
* Attempts per task: 4
* Functional and regression measurements: 392 / 392 trials
* Agent and trial duration measurements: 392 / 392 trials
* Trials with an exception category: 0

The empty Gemini `all-2` directory is excluded.
