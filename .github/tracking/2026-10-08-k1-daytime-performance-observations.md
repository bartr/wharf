# K=1 daytime performance observations

## Context

Two K=1, N=32 Wharf runs used the same deterministic, longest-agent-first
admission order:

- `all-1` ran overnight from 02:48 to 04:23 CDT.
- `all-2` started at 10:34 CDT and continued through US business hours.

The first 93 admission positions observed in both logs were identical and were
not alphabetical. Both runs therefore front-loaded the same historically slow
tasks.

## Completion timing

| Milestone | All-1 | All-2 | Difference |
| --- | ---: | ---: | ---: |
| 50 terminal trials | 61.3 min | 74.6 min | +13.3 min |
| 57 terminal trials | 63.7 min | 78.3 min | +14.6 min |
| 113 terminal trials | 94.7 min | In progress | — |

All-1 required another 31.0 minutes after terminal trial 57. Because both runs
used the same front-loaded order, the faster historical tail is relevant, but
it does not remove All-2's observed milestone lag.

All-1 performed no whole-trial retries. All-2 had performed two whole-trial
retries at the time of observation. Retries explain some, but not all, of the
difference.

## Matched stage timing

The comparison used 69 tasks that had reached a terminal result in All-2 and
had matching preparation, agent, and verifier lifecycle records in All-1.

| Stage | All-1 median | All-2 median | Ratio of medians | Median matched-task ratio |
| --- | ---: | ---: | ---: | ---: |
| Preparation | 17.0 sec | 26.5 sec | 1.56x | 1.61x |
| Agent | 1,261.2 sec | 1,508.7 sec | 1.20x | 1.19x |
| Verifier | 34.8 sec | 58.0 sec | 1.67x | 1.48x |

Four matched tasks had either a whole-trial or agent-infrastructure retry in
All-2. After excluding them:

- 65 matched tasks remained.
- All-1 median agent duration was 1,261.2 seconds.
- All-2 median agent duration was 1,491.4 seconds.
- The median matched-task agent ratio was 1.17x.
- All-2 was slower on 43 of 65 tasks.

This establishes a material daytime slowdown even after retry-affected tasks
are removed.

## Interpretation

The logs demonstrate correlation, not a single cause. US business-hour load on
Copilot is a plausible contributor because agent work dominates elapsed time,
but the slowdown is not isolated to the agent stage.

Preparation and verification also slowed materially. Likely contributors
include:

- daytime load on Copilot and other external services;
- slower container registries and Debian, Ubuntu, or language-package mirrors;
- `apt-get` and package restoration performed inside agent execution;
- Docker build, filesystem, and disk contention under N=32 and B=12;
- differences in cache warmth or upstream response time.

Agent duration includes commands executed inside the container, so package
mirror or build-infrastructure latency can appear as agent slowdown even when
Copilot response latency is unchanged.

## Implications

- Do not compare benchmark wall time across time-of-day windows as if the
  environment were controlled.
- Report retry counts alongside wall time, but do not attribute the full
  slowdown to retries.
- Use matched-task stage distributions rather than only total run duration.
- Treat a single overnight/daytime pair as directional evidence, not a causal
  benchmark.

## Recommended follow-up

1. Repeat equivalent K=1 runs in multiple overnight and business-hour windows.
2. Retain structured preparation, agent, verifier, and cleanup stage events.
3. Add substage timing for image inspection, pull, context preparation, build,
   and verifier-image preparation.
4. Record cache state and read-only host/Docker resource snapshots at run start
   and periodic intervals.
5. Separate Copilot request waiting, agent command execution, package download,
   and local build time where retained evidence permits it.
6. Compare medians, upper percentiles, matched-task ratios, retry counts, and
   completion curves rather than relying on a single average.
