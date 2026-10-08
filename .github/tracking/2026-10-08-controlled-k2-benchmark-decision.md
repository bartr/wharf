# Controlled K=2 benchmark decision

Decision time: 2026-10-08 15:36 CDT, before the K=2 run started and before any
new result was known.

## Decision

Exclude All-1 from comparative scoring because its execution contract is not
comparable to the hardened runner:

- it used the earlier memory policy;
- it predates explicit Docker OOM detection and bounded whole-trial retry;
- patch capture and failed-agent verification changed afterward;
- cost propagation was incomplete; and
- known pnpm/Koota containment defects remained.

Retain All-1 unchanged as pre-contract qualification evidence. Exclusion is a
protocol decision, not a reaction to its score.

Treat All-2 as transitional qualification evidence and All-3 as supporting
released `0.22.0` evidence. Do not combine either with the new run and label
the result as a homogeneous K=3 or K=4 cohort.

## Pre-registered run

- Host: z01.
- Source: pushed `0.23.0` code after this decision is committed.
- Dataset: fixed 113-task DeepSWE suite.
- Model: Copilot `gpt-5.6-luna`, max effort.
- K: 2.
- N: 32.
- P: 12.
- Trials: 226 before bounded infrastructure or whole-trial retries.
- Docker cache state: record immediately before launch and label the result
  warm or cold from that evidence.
- Execution: foreground process in a named tmux session.

Record the source commit, `wharf --version`, effective Copilot CLI version,
manifest, task checksum, start time, and Docker state with the run.

## Fixed interpretation

- Pass rate is passing trials divided by 226.
- Cost is reported as USD per task attempt, not aggregate run cost and not cost
  per pass.
- Report cost-evidence coverage as `costed attempts / 226`.
- Do not impute missing cost in the primary metric. Any estimate must be
  separate and explicitly labeled.
- Count and retain all OOMs, timeouts, agent-infrastructure retries,
  whole-trial retries, verifier failures, and missing-cost attempts.
- Do not exclude a task or attempt after observing its result unless this
  pre-registered contract already requires that exclusion.

## Comparison

Compare the controlled K=2 result directionally with Pier K=4 using normalized
pass rate and USD cost per task attempt. Keep K, host, concurrency, Copilot CLI
version, and cache-state differences visible. A later identical K=2 run is
required before presenting a homogeneous Wharf K=4 cohort.
