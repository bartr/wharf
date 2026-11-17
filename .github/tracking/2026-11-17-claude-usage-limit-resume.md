# Claude usage-limit recovery

## Research and plan

User requested resetting only the nine usage-limit errors in
`wharf-swe/jobs/claude-claude-opus-5-5-default-2.1.326/all-1` for later retry.
Baseline read-only resume planning reports completed=56, pending=0.

All nine have HTTP 429 `usage_limit_reached` in both original and final agent
stdout, terminal `agent-failed`, and one retained whole-trial retry. Their
original partial patches and costs remain in `trial-retries/retry-1`.
The 46 passes and the ordinary test failure must remain terminal.

`RetainedRunResumePlanner.cs:177` treats any valid root result.json as terminal.
`ExistingTrialRetry` and `ExistingTotalCost` at lines 302/316 recover retry
identity and previous cost independently. `ResumeEvidenceArchiver` at line 445
archives current execution evidence only when resume actually executes.

Move each selected root `result.json` to
`result.usage-limit-before-resume.json` in the same trial directory. Do not
modify its contents, agent evidence, retry folders, run log, or run plan.
This makes those logical trials unfinished for the existing resume mechanism
without deleting failure evidence or resetting retry/cost history.

Selected logical attempts:

- wharf-derived-image attempt 4
- wharf-execution attempts 2 and 4
- wharf-image-pull attempts 3 and 4
- wharf-packaging attempt 4
- wharf-recovery attempt 4
- wharf-reporting attempt 4
- wharf-scheduler attempt 3

Validate exactly nine selections, both rate-limit events, and destination
absence before any moves. Hash all run evidence before/after to verify only
the nine intended renames. Run read-only resume planning afterward and require
completed=47/pending=9 with exactly these logical attempts. Do not start a run.

## Operational constraints

Preserve all original failures for audit and later analytics. Retrying does not
create new logical trials or permit post-result exclusions. This action does
not reset the automatic retry budget or rewrite cost evidence.
Claude reports usage reset at 2026-11-18 01:40 UTC (November 17, 7:40 PM CST).

## Execution and review

Completed the nine renames after validating both original and final rate-limit
results. SHA-256 snapshots of all 1,709 run evidence files confirmed exactly
those nine path changes and no file-content changes. Remaining root results
are 46 passed and one ordinary failed verification.

Read-only current-build resume dry-run succeeded:

```text
resume completed=47 pending=9 K=4 N=56 P=12 model=claude-opus-5-5 effort=default cli=2.1.326
```

Its nine pending task/attempt identities match the selection above, each with
retained `trial-retry=1`. No benchmark execution was started.

After quota resets, run from the repository root in a named tmux session:

```bash
wharf resume --run-directory wharf-swe/jobs/claude-claude-opus-5-5-default-2.1.326/all-1
```

Unset obsolete `WHARF_CLI_VERSION`/`CLI_VERSION` first if still present.
The nine terminal results are intentionally pending until execution finishes;
do not treat interim analytics as a completed arm or exclude the retained
quota failures from its audit history.
