# Friendly analytics export: changes

Implements the approved [research](2026-11-25-friendly-analytics-export-research.md)
and [plan](2026-11-25-friendly-analytics-export-plan.md).

## Delivered behavior

- `wharf analytics export` defaults to `./jobs`, discovers every positive `all-N`
  run, and combines completed jobs into one sibling `data/<arm>.jsonl`.
- Incomplete/resumable runs, active writers, unfinished batches, pending quota
  rejection cleanup, and baseline configurations are skipped with reasons.
- Existing files are skipped before reading their job evidence. `--recompute`
  replaces eligible exports; an arm without completed jobs leaves its file alone.
- Retained configuration, arm directories, and plan logs supply labels. Explicit
  contradictory labels, including `--job`, fail before prepared exports are
  written. Unknown subagent availability stays null.
- Native checksums come from retained plans, not today's task checkout. Legacy
  unavailable full checksums stay null. A historical request for `latest` uses
  the arm's numeric effective version with an explicit warning.
- Logical identity is `(job, trial)` in both exports and manifests. Retry
  evidence is not exported as additional logical trials.
- New exports use atomic no-overwrite creation, including competing writers;
  recomputation preserves atomic replacement.
- Trailing directory separators are normalized. Discovery rejects multiple jobs
  trees to prevent cross-suite aggregation; default output follows the discovered
  suite even when traversal starts above its jobs directory.

Shared recovery helpers remain evidence-only; full resume still independently
loads and validates current tasks. Related CLI, operator documentation, source
requirements, PRD, and ledger are updated together. Published data/charts and
historical task payloads are unchanged.

## Validation

All long-running commands ran foreground in owned tmux session
`wharf-friendly-analytics`. No paid evaluations were launched.

- Focused Release suite: **116/116 succeeded**, zero failed/skipped. Covers
  discovery, exporter, manifest, CLI, recovery, state, and batch behavior;
  excludes `RunCommandExecutesTaskDiscovery`.
- Release solution build and Debug CLI build: exit 0.
- Formatting and verification for all ten changed C# files: exit 0.
- Actual user `wharf` command, from WharfSWE, exported/recomputed **75 arms/jobs,
  4,200 logical trials** into session scratch output: exit 0.
- Repeated export skipped existing files and reported zero exported trials:
  exit 0.
- Scratch manifest rebuild: exit 0; all 75 entries independently reconciled for
  row/identity/task counts, K=4 depth, passes, P2P errors, score, cost coverage,
  rounded costs/agent durations, and JSONL bytes.
- Decimal checks confirmed retained credits convert at exactly $0.01/credit.
  Cost/full-checksum coverage: 3,640/4,200 each; agent/trial-duration coverage:
  4,200/4,200 each. Missing evidence was not imputed.
- All **12,761 retained source-evidence hashes** and **75 export hashes**
  remained unchanged after final recomputation.
- Requirements ledger: 394 unique records and matching source SHA-256.
- Whitespace checks passed; tracked task, raw-job, and published-data paths
  remained unchanged.
- Owned scratch exports, hash snapshots, and verification script were removed
  after reconciliation. The finished owned tmux session was closed; unrelated
  sessions were left alone. Changes remain uncommitted.

## Failures retained in this record

Initial real discovery rejected an older native plan's requested `latest`
against its numeric arm version. The request/effective distinction was corrected
without editing retained evidence, then real validation passed.

The final independent reconciliation script initially compared unrounded cost
sums with the existing rounded manifest presentation. Its assertion failed
(20.0578274 raw versus 20.06 displayed). The checker was corrected to the existing
two-decimal total, four-decimal per-trial, and one-decimal duration contract;
all entries then passed. No application change was needed for this discrepancy.

## Limits

These checks establish offline discovery/export behavior, not provider quality,
benchmark speed, or release-package qualification. Existing status-derived reward
fallbacks and general manifest semantic hardening remain separate work. Existing
56-trial chart methodology is not extended to combined multi-job arms. The known
pre-existing OQ-002 ledger metadata inconsistency is unchanged.
