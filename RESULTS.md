# DeepSWE Results

## Headline findings

- **The harness materially changes both quality and cost.** With Claude Opus 5,
  the GitHub Copilot agentic harness reached **68.5%**, 4.1 percentage points
  above Claude Code's **64.4%**, while reducing average cost per trial from
  **$13.40 to $5.77** (**57.0% lower**).
- **Luna delivered the strongest value observed here.** GPT-5.6 Luna with
  Copilot reached **72.8%**, compared with **67.2%** for the same model under
  the DeepSWE mini-swe-agent harness, while costing **$0.37 instead of $3.03
  per trial** (**87.9% lower**).
- **GPT-6 Astra with Copilot reached 321/452 (71.0%).** The published DeepSWE
  mini-swe-agent arm for the same model, max effort, task suite, and K=4 passed
  331/452 (73.2%), a nominal 2.21-point lead for mini-swe-agent. Six local
  trials ended in agent or verifier errors; excluding them raises the local
  rate to 72.0% and does not reverse the comparison. Among rows with cost data,
  Copilot averaged **$8.43 per trial** (447/452 rows), compared with **$7.50**
  for mini-swe-agent (452/452), making Copilot **12.4% more expensive**.
- Meta reported **64.8%** for GPT-5.6 Terra with Codex, its selected GPT agent
  product. Luna with Copilot scored 8.0 percentage points higher. This is a
  product-harness comparison, not a same-model comparison.
- **The existing Terra run provides the direct Meta comparison.** Terra with
  Copilot passed **376/565 (66.5%)** at K=5. Meta's rounded **64.8%** Terra +
  Codex headline is consistent with **366/565**: about ten additional passes
  for Copilot with the same model, task suite, and K. The 1.77-point difference
  is smaller than the local Terra run's 3.29-point task-clustered standard
  error, so treat the configurations as close rather than claiming a decisive
  Copilot advantage.
- Luna with Copilot also exceeded Opus 5 with Copilot by 4.3 percentage points
  while costing 93.6% less per trial ($0.37 vs. $5.77).
- As secondary quality context, Luna with Copilot tied the 72.8% DeepSWE Opus
  5 mini-swe-agent reference at substantially lower cost, but the different
  model and harness make that a quality-cost frontier observation rather than
  the primary comparison.
- **Auto model selection performed poorly on this workload.** The Copilot Auto
  arm passed only **209/565 trials (37.0%)**, 35.8 percentage points below Luna
  and 31.5 points below Opus 5 with Copilot. Excluding its 27 agent timeouts
  would raise the rate only to 38.8%, so timeouts do not explain the gap.
  The Copilot team is developing a new Auto implementation called
  **Hydrafusion**; we will rerun the full benchmark when access becomes
  available.

These are aggregate benchmark comparisons, not paired claims that every task
or rollout improved. They nevertheless show why an agent-model configuration
must be evaluated as a unit: the harness and model choice can shift the
quality-cost frontier substantially.

## DeepSWE Performance

| Model | DeepSWE | Copilot | Claude Code | Muse | Meta |
|---|---:|---:|---:|---:|---:|
| Opus-5-max | 72.8% | 68.5% | 64.4% | | 65.0% |
| Terra-max | 69.6% | 66.5%† | | | 64.8% |
| Luna-max | 67.2% | 72.8% | | | |
| Astra-max | 73.2% | 71.0%§ | | | |
| Muse-xhigh | | | | 59.8% | 59.3% |
| Auto-max | | 37.0%‡ | | | |

† API content safety filter blocked 2 tasks

‡ Auto completed all 565 planned trials across 113 tasks. The export retains
`model: auto` and does not identify the model selected for each trial.
Hydrafusion has not yet been evaluated in this repository.

§ Astra completed all 452 planned trials across 113 tasks. Its six error
outcomes remain in the official full-denominator PASS@1 calculation.

Meta's methodology pairs GPT-5.6 Terra with Codex. Meta did not report Luna,
so the 72.8% Luna-with-Copilot versus 64.8% Terra-with-Codex comparison changes
both the model and harness.

### Cross-evaluation corroboration

Our evaluation, Meta's DeepSWE evaluation, and GitHub's published harness
evaluation all test models with their selected or model-vendor agentic
harnesses rather than treating model output in isolation. On the two DeepSWE
configurations that overlap with Meta, our aggregate results closely reproduce
theirs:

- Muse Spark 1.2 with Muse Code: **338/565 locally**. Meta's rounded 59.3%
  headline is consistent with **335/565** under a full denominator: a difference
  of only **three passes across 565 attempts**.
- Claude Opus 5 with Claude Code: **291/452 locally**. Meta's rounded 65.0%
  K=5 headline is consistent with **367/565**; the local rate corresponds to
  about 364 passes on that same 565-attempt scale, again roughly three pass
  equivalents.

Meta ran K=5. The local Claude Code arm is K=4, while the local Muse arm is
also K=5. Different K values change sampling precision and the possible rounded
rates, not the expected PASS@1. The exact local Claude Code rate is 291/452 =
64.3805%; if Meta's rounded Opus headline represents 367/565, the inferred gap
is about 0.58 percentage points. Meta did not publish the rows needed to verify
that numerator or denominator policy.

The three-pass Muse difference is small relative to observed DeepSWE
variability. In our K=5 Muse run, **65/113 tasks (57.5%)** had mixed outcomes
between 1/5 and 4/5; only 48 tasks landed at either 0/5 or 5/5. Three trials are
0.53 percentage points, compared with an approximately 2.1-point naive
trial-level standard error and a 3.3-point task-clustered standard error in the
local run.

This agreement across separately operated evaluation infrastructure supports
the credibility of the task execution, harness selection, and grading
methodology. At the available reporting precision, both overlapping results are
effectively tied, and the observed difference is not meaningful relative to
the benchmark's trial-to-trial variation. This is corroboration rather than a
formal equivalence test or replication: Meta did not publish task-level results
or exact pass counts, and GitHub evaluated the same model-vendor-harness
principle on different benchmark suites rather than reporting these DeepSWE
configurations.

## DeepSWE Average Cost per Trial

| Model | DeepSWE | Copilot | Delta |
|---|---:|---:|---:|
| Opus-5-max | $11.84 | $5.77 | -51.3% |
| Terra-max | $4.95 | incomplete§ | — |
| Luna-max | $3.03 | $0.37 | -87.9% |
| Astra-max | $7.50 | $8.43¶ | +12.4% |

| Model | Claude Code | Copilot | Delta |
|---|---:|---:|---:|
| Opus-5-max | $13.40 | $5.77 | -57.0% |

Auto cost is omitted because 27 of 565 exported trials have no cost value.

§ Terra has cost values for 551/565 trials. The previous $4.15 figure divided
the incomplete total by all 565 trials and understated average cost; it is not
a valid complete-arm comparison.

¶ Astra's Copilot average excludes 5/452 rows without cost data and divides
the observed $3,767.03 by the 447 cost-bearing rows. The DeepSWE average uses
all 452 rows ($3,389.02 total).
