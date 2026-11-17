# Copilot 1.0.113 WharfSWE analytics changes

## Implementation

Exported five completed `all-1` runs with canonical `wharf analytics export`,
explicit model, `copilot-cli` identity, version `1.0.113`, and default effort.
Each new export contains 56 logical trials across the same 14 task checksums,
K=4, N=56, P=12. No retries were emitted as extra trials.

Rebuilt `wharf-swe/data/manifest.json` with `wharf analytics manifest
--data-directory wharf-swe/data`: 52 arms and 2,912 trials.
Regenerated the three existing SVG charts using
`python3 scripts/plot-wharf-swe-pass-at-1.py`: 46 arms across five models.
Updated `wharf-swe/README.md` with the latest comparison, complete sorted
results, coverage totals, and date; retained the historical 1.0.112 comparison.

| Model | Passes / trials | P2P-error trials | Score | Cost/costed trial | Average agent seconds |
|---|---:|---:|---:|---:|---:|
| GPT-6.1 Sol | 56 / 56 | 0 | 100.00% | $0.2082 | 330.5 |
| GPT-6 Astra | 56 / 56 | 0 | 100.00% | $1.1352 | 296.5 |
| HydraFusion | 55 / 56 | 0 | 98.21% | $0.2531 | 388.5 |
| GPT-6 Luna | 53 / 56 | 1 | 94.39% | $0.0157 | 274.6 |
| Claude Opus 5.5 | 51 / 56 | 2 | 90.57% | $0.4935 | 206.6 |

## Validation

Independent Python checks reconciled all 280 new rows against retained
run plans, results, agent summaries, verifier rewards, stage timestamps,
patch byte counts, and effective CLI versions. Every arm has 56 unique
logical-trial identities, four attempts per task, complete cost/timing
evidence, no exception categories, and no whole-trial/verifier retries.
All trials remain in the denominator. Credit conversion uses exact decimal
arithmetic at $0.01 per credit.

The initial validation probe compared an integer credit divided using
Python float arithmetic against Decimal USD and failed on one 1-credit
row. Explicit Decimal conversion corrected the validator; all five arms
then passed without modifying exports or evidence.

Independently checked all 52 README result rows against manifest values
and ordering, complete 2,912-trial measurement coverage, and all 46 plotted
pass-count, cost-per-trial, and inclusive-p90 duration values in each SVG.
All historical tracked JSONL exports match HEAD byte-for-byte; DeepSWE
has no tracked changes. `git diff --check` passed.

No product code changed, so no build or runtime test suite was necessary.
No benchmark, model call, installation, or commit was performed. Raw jobs,
the pending Claude Code run, and the user's untracked `wharf-swe/run.sh`
were not modified.
