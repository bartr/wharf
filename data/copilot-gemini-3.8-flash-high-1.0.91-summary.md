# Gemini 3.8 Flash High Benchmark Summary

Generated 2026-10-06 from `copilot-gemini-3.8-flash-high-1.0.91/all-1`.

## Headline

| Metric | Result |
|---|---:|
| Benchmark score (PASS@1) | **74.1%** |
| Passed trials | 335 / 452 |
| Tasks | 113 |
| Attempts per task | 4 |
| Total cost | **$1,260.95** |
| Cost coverage | 452 / 452 trials |
| Average cost per trial | **$2.79** |
| Average cost per task (4 attempts) | **$11.16** |
| Cost per passing trial | **$3.76** |
| Copilot credits | 126,095.175 |
| Premium requests | 6,174 |

PASS@1 is the trial-level pass rate and is the benchmark headline metric. As
supplemental context, 103 of 113 tasks (91.2%) had at least one passing attempt
among their four trials; this is not the PASS@1 score.

## By language

| Language | Tasks | Passes / trials | PASS@1 | Total cost | Cost / trial | Cost / pass |
|---|---:|---:|---:|---:|---:|---:|
| Go | 34 | 113 / 136 | 83.1% | $350.14 | $2.57 | $3.10 |
| Python | 34 | 90 / 136 | 66.2% | $357.09 | $2.63 | $3.97 |
| TypeScript | 35 | 101 / 140 | 72.1% | $417.11 | $2.98 | $4.13 |
| JavaScript | 5 | 16 / 20 | 80.0% | $58.38 | $2.92 | $3.65 |
| Rust | 5 | 15 / 20 | 75.0% | $78.23 | $3.91 | $5.22 |
| **Overall** | **113** | **335 / 452** | **74.1%** | **$1,260.95** | **$2.79** | **$3.76** |

## Accounting notes

- Every trial has a score and Copilot spend record.
- Cost was recovered from each trial's Copilot `session.shutdown` accounting
  event using `cost_usd = totalNanoAiu / 100,000,000,000`.
- Recorded usage totals are 9,017,390,474 input tokens and 68,558,287 output
  plus reasoning tokens.
- Values above are observed Copilot accounting values; no missing-cost
  imputation was required.
