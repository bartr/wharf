# Enterprise Score: Frontier-Effort Comparison

Generated 2026-10-06 from exported trial analytics.

## Definition

Each trial receives:

```text
0, if measurements are missing or any pre-existing test regresses

otherwise:
80 * (new tests passed / new tests total) + 20 * full_pass
```

Attempts are averaged within each task, then task scores are averaged. Every
task therefore has equal weight regardless of its number of tests or attempts.

A regression-free implementation that passes 98% of new tests scores 78.4. A
complete passing implementation scores 100.

## Results

| Arm | Tasks | Trials | Measured | Regression-free | Coverage points (80) | PASS points (20) | Enterprise score |
|---|---:|---:|---:|---:|---:|---:|---:|
| Luna 5.6 frontier, CLI 1.0.77 | 113 | 452 | 451 | 96.0% | 73.88 | 14.82 | **88.71** |
| Gemini 3.8 Flash frontier, CLI 1.0.91 | 113 | 452 | 450 | 95.1% | 72.71 | 14.82 | **87.54** |
| Luna 5.6 frontier, CLI 1.0.91 | 113 | 452 | 451 | 95.1% | 73.17 | 13.98 | **87.16** |

The coverage and PASS columns are weighted contributions to the 100-point
score, not standalone percentages.

## Interpretation

- Luna 5.6 on CLI 1.0.77 leads by 1.17 points over Gemini and by 1.55 points
  over Luna on CLI 1.0.91.
- Gemini and Luna 1.0.77 receive the same 14.82-point full-completion
  contribution because both passed 335 of 452 trials.
- Luna 1.0.91 earns more partial-coverage value than Gemini, but its lower
  full-completion rate offsets that advantage.
- The enterprise score is a local utility metric, not a DeepSWE leaderboard
  score. PASS@1 should continue to be reported separately.
