# Controlled K=2 benchmark result

This result applies the interpretation registered before launch in
`2026-10-08-controlled-k2-benchmark-decision.md`. No task or attempt is
excluded after observing the outcome.

## Run identity

- Host: z01
- Source commit: `af3574f2dcfedad3d9390cb5cdae3e8f86f65790`
- Wharf version: `0.23.0`
- Model: Copilot `gpt-5.6-luna`, max effort
- Dataset: fixed 113-task DeepSWE suite
- Configuration: K=2, N=32, P=12
- Cache state: warm
- Started: 2026-10-08 15:41:46 CDT
- Finished: 2026-10-08 18:58:07 CDT
- Wall time: 3:16:20

## Primary metrics

| Metric | Result |
| --- | --- |
| Passing attempts | 156/226 |
| Pass rate | 69.03% |
| Attempt 1 | 80/113 (70.80%) |
| Attempt 2 | 76/113 (67.26%) |
| Known cost | $78.2231 |
| Cost evidence coverage | 220/226 (97.35%) |
| Cost per costed task attempt | $0.3556 |

The six missing-cost attempts remain in the 226-attempt pass denominator. All
six passed verification using retained patches: four agent executions were
cgroup OOM-killed and two reached the agent timeout.

## Task-level overlap

| Outcome across K=2 | Tasks |
| --- | ---: |
| Passed both attempts | 61 |
| Passed only attempt 1 | 19 |
| Passed only attempt 2 | 15 |
| Passed neither attempt | 18 |
| Passed at least once | 95/113 (84.07%) |

## Retry and failure evidence

The run used the whole-trial retry behavior loaded from `af3574f`:

- four whole-trial retries;
- three verifier timeouts;
- one verifier infrastructure failure;
- three retried trials ultimately passed;
- `pwntools-tube-multiplexing` attempt 1 remained failed after retry.

Verifier-only retry commit `fcc8235` was created after this process launched
and did not affect the run.

## Comparison

- All-2 and All-3 each passed 84/113 (74.34%). Repeating that aggregate would
  be 168/226, so this controlled K=2 result is 12 passes and 5.31 percentage
  points lower.
- All-2 and All-3 together passed at least one attempt on 100/113 tasks
  (88.50%), versus 95/113 (84.07%) here.
- Pier K=4 passed 314/452 (69.47%). Wharf is 0.44 percentage points lower.
- Pier cost approximately $0.367 per costed attempt. Wharf's $0.3556 is about
  3.1% lower.

The controlled result does not reproduce the pass-rate advantage suggested by
the transitional/supporting Wharf runs. It is effectively tied with Pier on
pass rate while retaining a small cost advantage. Individual controlled
attempts are not statistically distinguishable from All-2 or All-3 in paired
exact comparisons, so the lower aggregate is consistent with meaningful model
variance; the fixed 156/226 result nevertheless remains the primary score.
