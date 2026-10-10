# GitHub Copilot agentic harness methodology

Source:

- GitHub Blog, [Evaluating performance and efficiency of the GitHub Copilot
  agentic harness across models and
  tasks](https://github.blog/ai-and-ml/github-copilot/evaluating-performance-and-efficiency-of-the-github-copilot-agentic-harness-across-models-and-tasks/)
  (accessed 2026-09-28)

GitHub describes the shared component that orchestrates tools, context, and
workflow as the **GitHub Copilot agentic harness**. The post evaluates that
harness across models and benchmark tasks and compares it with
**model-vendor harnesses**, such as Claude Code and Codex CLI.

## Terminology used in this repository

Use GitHub's nomenclature where it describes the same concept:

| Preferred term | Meaning here |
| --- | --- |
| agentic harness | The orchestration layer around a model, including tools, context, and workflow |
| model-vendor harness | A harness shipped by the model provider, such as Claude Code or Codex CLI |
| agent-model configuration | A particular harness/agent and model pairing, including relevant settings |
| task resolution or task completion | Whether the benchmark verifier accepts the completed task |
| token efficiency | Token consumption in relation to task resolution |
| cost per task | Dollar cost aggregated at the benchmark-task level |
| run-to-run variability | Variation between independent repetitions of an evaluation |

DeepSWE-specific data terms remain unchanged:

- A **task** is one benchmark problem.
- A **trial** or **rollout** is one attempt at one task. These terms correspond
  to the DeepSWE data schema and are not interchangeable with a complete
  independent benchmark run.
- An **arm** is a repository-local grouping of trials for an agent-model
  configuration and its fixed settings.
- **PASS@1** is reported according to the benchmark-specific aggregation
  documented in [../ANALYTICS.md](../ANALYTICS.md), not inferred from the
  GitHub post.

Use **task resolution rate** in prose about whether work is completed. Retain
**pass rate** where it names a benchmark metric, table column, or verifier
outcome.

## Methodology principles to carry forward

For a harness comparison, hold the model and benchmark task fixed and normalize
the context window, reasoning effort, tool selection, and MCP servers. Report
task resolution alongside efficiency rather than treating lower token or dollar
cost as sufficient by itself. Quantify stochastic variability with repeated
runs and distinguish model-generated failures from infrastructure anomalies.

The GitHub post is context for terminology and experimental design, not a
DeepSWE baseline. Its published results use different benchmarks, settings,
exclusion rules, and aggregation choices, so their numeric results must not be
compared directly with this repository's arms.

## Cross-evaluation corroboration

Three separately operated evaluations use the same broad comparison unit: a
model together with its selected or model-vendor agentic harness.

1. This repository evaluates DeepSWE with combinations including Claude Opus 5
   plus Claude Code and Muse Spark 1.2 plus Muse Code.
2. Meta's DeepSWE evaluation selects Claude Code for Opus and Muse Code for
   Muse.
3. GitHub compares its agentic harness with model-vendor harnesses while
   holding the model and benchmark task fixed.

The direct DeepSWE overlap is between this repository and Meta. Our Muse Spark
1.2 plus Muse Code result is 338/565; Meta's rounded 59.3% is consistent with
335/565, a difference of only three passes across 565 attempts. Our Claude
Opus 5 plus Claude Code result is 291/452; Meta's rounded 65.0% K=5 headline is
consistent with 367/565, while the local rate corresponds to about 364 pass
equivalents on that denominator. Agreement this close across different
evaluation infrastructure lends credibility to the shared task-execution and
grading methodology.

Meta ran K=5. The local Claude Code result is 291/452 at K=4; the local Muse
result is 338/565 at K=5. Different K values change rate granularity and
sampling precision but do not inherently change expected PASS@1.

The local Muse run also shows substantial stochastic variation: 65/113 tasks
had mixed outcomes between 1/5 and 4/5. A three-pass difference is only 0.53
percentage points, well below the local run's approximately 2.1-point naive
trial-level standard error and 3.3-point task-clustered standard error.

The strongest same-model external harness comparison is already available for
GPT-5.6 Terra at K=5. Terra with Copilot passed 376/565 trials (66.5%). Meta's
rounded 64.8% Terra-with-Codex result is consistent with 366/565, a nominal
ten-pass or 1.77-point Copilot lead. The local Terra arm has mixed outcomes on
60/113 tasks and a 3.29-point task-clustered standard error, so describe the
harnesses as close rather than treating the nominal lead as decisive.

This is aggregate corroboration, not a formal paired replication. Meta did not
publish exact pass counts or task-level results for these comparisons, so the
Meta numerators above are inferred from rounded percentages. GitHub's post
evaluates the harness principle on different benchmark suites rather than
providing a third DeepSWE result.
