# WharfSWE

WharfSWE is a 14-task C# software-engineering benchmark built from Wharf's own
development history. The Wharf CLI runs this suite separately from DeepSWE.
At K=4, each arm contains 56 logical trials.

## Task admission order

Wharf automatically loads [duration estimates](tasks/agent-duration-estimates.json)
and admits tasks longest to shortest within each K wave. Estimates use all
168 logical trials from the checksum-matched HydraFusion, GPT-6.1 Sol, and
GPT-6 Luna CLI 1.0.112 arms described below: 12 observations per task, including
reward-zero trials. Agent seconds come from the retained JSONL exports;
verifier seconds are the sum of command durations in each retained
`verifier/trial-result.json`. Each scheduling estimate is the sum of the
per-task median agent and median verifier seconds, rounded to milliseconds.
Preparation and evidence-copy time are excluded.

The shared schema's C# language stride is 1, so it introduces no language
interleaving or artificial duration offsets. `--shortest-first` reverses the
order within each wave. These estimates do not change task payloads/checksums.

## Passing trials by CLI revision

![Passing trials by Copilot CLI revision for HydraFusion, GPT-6.1 Sol, GPT-6 Astra, GPT-6 Luna, and Claude Opus 5.5](docs/pass-at-1-by-cli-revision.svg)

The graph includes HydraFusion, GPT-6.1 Sol, GPT-6 Astra, GPT-6 Luna, and
Claude Opus 5.5. Each point is one completed 56-trial arm. The X axis starts at
revision 95 and uses only the revision of `1.0.x`. The Y axis shows the number
of passing logical trials out of 56, not a rate or the regression-adjusted
Score, and is zoomed to 48-56 to include every observed count.
Lines connect observed runs; missing revisions are not
additional measurements. Small samples and changes in host/preparation settings
mean these are descriptive comparisons, not isolated CLI-version effects.
Seven historical revision-95 arms retain different task checksums from the
recent runs, so the full history is not a checksum-matched experiment.

## Cost per test by CLI revision

![Mean USD cost per costed trial by Copilot CLI revision](docs/cost-per-trial-by-cli-revision.svg)

Cost/test is total retained USD cost divided by costed logical trials, not cost
per passing trial. All plotted arms have 56/56 cost coverage; missing costs are
never imputed. The model selection and revision-95 starting point match the
passing-trials graph.

## p90 agent duration by CLI revision

![p90 agent-command duration in minutes and seconds by Copilot CLI revision](docs/agent-duration-by-cli-revision.svg)

Duration is the 90th percentile of the 56 actual agent-command durations per
arm, calculated using linear interpolation, with Y-axis labels in `m:ss`.
Each trial's duration includes agent time across whole-trial retries but excludes
preparation, evidence copying, and verifier time. All plotted arms have 56/56
duration coverage. Models, revision range, and comparison caveats match the
other graphs.

Regenerate all three graphs in `wharf-swe/docs/` from the JSONL exports in
`wharf-swe/data/` with `python3 scripts/plot-wharf-swe-pass-at-1.py` from the
repository root. Keep this README aligned with `data/manifest.json`.

## Latest CLI 1.0.112 comparison

Sources: [HydraFusion](jobs/copilot-hydrafusion-default-1.0.112/all-1),
[GPT-6.1 Sol](jobs/copilot-gpt-6.1-sol-default-1.0.112/all-1), and
[GPT-6 Luna](jobs/copilot-gpt-6-luna-default-1.0.112/all-1).
All three completed runs use identical task IDs/checksums and retained
configuration apart from model: K=4, N=40, P=16, and default effort.
Retained image-cache metadata confirms effective CLI 1.0.112 for all
168 trials. These are sequential runs, not replicated model comparisons;
host load and cache conditions are not controlled by matching configuration.

| Metric | HydraFusion | GPT-6.1 Sol | GPT-6 Luna |
|---|---:|---:|---:|
| Passes / logical trials | 56 / 56 | 56 / 56 | 51 / 56 |
| PASS@1 | 100.00% | 100.00% | 91.07% |
| P2P errors | 0 | 0 | 3 |
| Score | 100.00% | 100.00% | 90.32% |
| Tasks solved on any retained attempt | 14 / 14 | 14 / 14 | 14 / 14 |
| Cost coverage | 56 / 56 | 56 / 56 | 56 / 56 |
| Total known cost | $14.3689 | $10.4845 | $0.7554 |
| Cost per costed trial | $0.2566 | $0.1872 | $0.0135 |
| Mean agent time | 299.4 s | 191.5 s | 170.9 s |
| Median agent time | 222.7 s | 182.9 s | 165.4 s |
| Agent p90 | 471.0 s | 303.5 s | 249.2 s |
| Agent p95 | 1042.2 s | 338.6 s | 263.1 s |
| Maximum agent time | 1230.6 s | 385.9 s | 283.6 s |
| Agent commands over 10 minutes | 4 / 56 | 0 / 56 | 0 / 56 |
| Agent commands over 15 minutes | 4 / 56 | 0 / 56 | 0 / 56 |
| Run elapsed through final cleanup | 22m 11.4s | 8m 38.5s | 7m 55.3s |
| Time to finish first 50 of 56 trials | 9m 15.0s | 6m 39.1s | 6m 29.7s |
| Remaining time for final six trials | 12m 56.4s | 1m 59.4s | 1m 25.6s |

HydraFusion and Sol pass every trial without measured regressions. HydraFusion
has a heavier observed agent-time tail: four commands exceed 15 minutes, while
every Sol and Luna command finishes within seven minutes. Sol has lower cost
per costed trial and lower mean agent time than HydraFusion in these runs.
Luna is cheaper and faster on average, but has five reward-zero trials,
including three patches with P2P errors.

Luna's reward-zero trials are `wharf-audit` attempt 3 and `wharf-host-profile`
attempts 1 and 3 (each 0/3 functional tests and 0/1 regression tests),
`wharf-reporting` attempt 4 (2/3 functional tests and 1/1 regression tests),
and `wharf-scheduler` attempt 3 (1/3 functional tests and 1/1 regression tests).
None of the three runs has whole-trial retries, verifier retries, or exception
categories; all trials remain in the denominator.

Timing percentiles use linear interpolation over the 56 actual agent-command
durations. Run elapsed spans the retained run-start timestamp to final cleanup;
the final-six interval starts when the 50th cleanup completes and includes
any remaining scheduling, preparation, agent execution, verification, and
cleanup. It is not a pure agent-time measure or an average trial duration.

## Data coverage

* Exported arms: 47
* Exported logical trials: 2632
* Unique tasks per arm: 14
* Attempts per task: 4
* Functional and regression measurements: 2632 / 2632 trials
* Agent and trial duration measurements: 2632 / 2632 trials
* Cost evidence: 2632 / 2632 trials
* Trials with an exception category: 0

Only completed `all-1` runs are included.
Infrastructure and verifier retries are not additional logical trials.

## Results

Verified 2026-11-16 from forty-seven completed runs. Each arm contains 14
tasks and four attempts per task.

Sorted by PASS@1 descending, then P2P errors ascending, then cost ascending.
All runs use default effort and the Copilot CLI.

| Model | Effort | CLI | CLI version | Passes | PASS@1 | P2P errors | Score | Cost/trial | Avg agent time |
|---|---|---|---|---:|---:|---:|---:|---:|---:|
| GPT-6.1 Sol | default | copilot-cli | 1.0.112 | 56 / 56 | 100.00% | 0 | 100.00% | $0.1872 | 191.5 s |
| GPT-6.1 Sol | default | copilot-cli | 1.0.110 | 56 / 56 | 100.00% | 0 | 100.00% | $0.1934 | 199.3 s |
| GPT-6.1 Sol | default | copilot-cli | 1.0.108 | 56 / 56 | 100.00% | 0 | 100.00% | $0.2011 | 243.4 s |
| GPT-6.1 Sol | default | copilot-cli | 1.0.111 | 56 / 56 | 100.00% | 0 | 100.00% | $0.2020 | 196.6 s |
| **HydraFusion** | default | copilot-cli | 1.0.111 | 56 / 56 | 100.00% | 0 | 100.00% | $0.2247 | 303.1 s |
| **HydraFusion** | default | copilot-cli | 1.0.112 | 56 / 56 | 100.00% | 0 | 100.00% | $0.2566 | 299.4 s |
| GPT-6 Astra | default | copilot-cli | 1.0.108 | 56 / 56 | 100.00% | 0 | 100.00% | $1.2170 | 246.4 s |
| GPT-6 Astra | default | copilot-cli | 1.0.102 | 56 / 56 | 100.00% | 0 | 100.00% | $1.2351 | 254.9 s |
| GPT-6.1 Sol | default | copilot-cli | 1.0.105 | 55 / 56 | 98.21% | 0 | 98.21% | $0.2298 | 224.8 s |
| **HydraFusion** | default | copilot-cli | 1.0.109 | 55 / 56 | 98.21% | 0 | 98.21% | $0.2409 | 349.3 s |
| **HydraFusion** | default | copilot-cli | 1.0.108 | 55 / 56 | 98.21% | 0 | 98.21% | $0.2802 | 347.9 s |
| GPT-6.1 Sol | default | copilot-cli | 1.0.109 | 55 / 56 | 98.21% | 1 | 97.96% | $0.2045 | 242.2 s |
| GPT-6.1 Sol | default | copilot-cli | 1.0.102 | 55 / 56 | 98.21% | 1 | 97.96% | $0.2307 | 333.9 s |
| **HydraFusion** | default | copilot-cli | 1.0.107 | 54 / 56 | 96.43% | 0 | 96.43% | $0.2661 | 331.5 s |
| **HydraFusion** | default | copilot-cli | 1.0.98 | 54 / 56 | 96.43% | 0 | 96.43% | $0.4116 | 234.8 s |
| GPT-6 Astra | default | copilot-cli | 1.0.105 | 54 / 56 | 96.43% | 0 | 96.43% | $1.3178 | 174.0 s |
| GPT-6 Astra | default | copilot-cli | 1.0.95 | 54 / 56 | 96.43% | 0 | 96.43% | $1.3180 | 252.5 s |
| GPT-6 Luna | default | copilot-cli | 1.0.108 | 54 / 56 | 96.43% | 1 | 96.18% | $0.0165 | 211.3 s |
| **HydraFusion** | default | copilot-cli | 1.0.96 | 54 / 56 | 96.43% | 1 | 96.18% | $0.3197 | 272.5 s |
| GPT-6 Luna | default | copilot-cli | 1.0.95 | 54 / 56 | 96.43% | 2 | 95.93% | $0.0158 | 257.5 s |
| GPT-6.1 Sol | default | copilot-cli | 1.0.95 | 54 / 56 | 96.43% | 2 | 95.93% | $0.2200 | 283.7 s |
| **HydraFusion** | default | copilot-cli | 1.0.110 | 53 / 56 | 94.64% | 0 | 94.64% | $0.2324 | 296.0 s |
| **HydraFusion** | default | copilot-cli | 1.0.100 | 53 / 56 | 94.64% | 1 | 94.39% | $0.2932 | 272.0 s |
| **HydraFusion** | default | copilot-cli | 1.0.97 | 52 / 56 | 92.86% | 1 | 92.61% | $0.3416 | 316.4 s |
| **HydraFusion** | default | copilot-cli | 1.0.101 | 52 / 56 | 92.86% | 1 | 92.61% | $0.3527 | 218.9 s |
| **HydraFusion** | default | copilot-cli | 1.0.105 | 52 / 56 | 92.86% | 2 | 92.36% | $0.2681 | 188.7 s |
| Claude Opus 5.5 | default | copilot-cli | 1.0.91 | 52 / 56 | 92.86% | 3 | 92.11% | $0.4792 | 180.8 s |
| Claude Opus 5.5 | default | copilot-cli | 1.0.105 | 52 / 56 | 92.86% | 3 | 92.11% | $0.5319 | 147.2 s |
| **HydraFusion** | default | copilot-cli | 1.0.106 | 51 / 56 | 91.07% | 0 | 91.07% | $0.2895 | 378.2 s |
| **HydraFusion** | default | copilot-cli | 1.0.95 | 51 / 56 | 91.07% | 0 | 91.07% | $0.3186 | 302.9 s |
| GPT-6 Luna | default | copilot-cli | 1.0.111 | 51 / 56 | 91.07% | 2 | 90.57% | $0.0137 | 200.0 s |
| **HydraFusion** | default | copilot-cli | 1.0.102 | 51 / 56 | 91.07% | 2 | 90.57% | $0.3341 | 266.9 s |
| **HydraFusion** | default | copilot-cli | 1.0.103 | 51 / 56 | 91.07% | 2 | 90.57% | $0.3930 | 229.4 s |
| GPT-6 Luna | default | copilot-cli | 1.0.112 | 51 / 56 | 91.07% | 3 | 90.32% | $0.0135 | 170.9 s |
| GPT-6 Luna | default | copilot-cli | 1.0.110 | 51 / 56 | 91.07% | 3 | 90.32% | $0.0141 | 183.0 s |
| **HydraFusion** | default | copilot-cli | 1.0.104 | 51 / 56 | 91.07% | 3 | 90.32% | $0.4028 | 196.7 s |
| GPT-6 Luna | default | copilot-cli | 1.0.109 | 50 / 56 | 89.29% | 2 | 88.79% | $0.0140 | 177.1 s |
| Gemini 3.8 Flash | default | copilot-cli | 1.0.95 | 50 / 56 | 89.29% | 3 | 88.54% | $0.8911 | 801.5 s |
| GPT-6 Luna | default | copilot-cli | 1.0.102 | 50 / 56 | 89.29% | 4 | 88.29% | $0.0152 | 258.3 s |
| Claude Opus 5.5 | default | copilot-cli | 1.0.95 | 49 / 56 | 87.50% | 5 | 86.25% | $0.5310 | 215.9 s |
| Claude Opus 5 | default | copilot-cli | 1.0.91 | 48 / 56 | 85.71% | 2 | 85.21% | $1.0990 | 230.7 s |
| Claude Opus 5.5 | default | copilot-cli | 1.0.108 | 48 / 56 | 85.71% | 3 | 84.96% | $0.4987 | 148.9 s |
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
