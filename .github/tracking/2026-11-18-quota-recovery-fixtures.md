# Opus and Sol quota-recovery fixtures

## Authorization and scope

The user requested preparing the actual retained runs before code changes,
and explicitly confirmed: "Delete the nine pending Opus payloads too".
This supersedes the specification's original Opus raw-evidence retention rule,
not the unchanged 47-terminal/nine-pending logical state.

No runtime source, pinned tasks, or unrelated results were changed.
No agents were launched. No generic batch-state format was invented before
implementation research; the durable audit identifies invalidated slots.

## Audit before deletion

The [generated audit](2026-11-18-quota-recovery-fixtures-audit.json) was flushed
to disk before deletion. It records exact selected paths, logical identities,
K/N/P/model/effort/effective CLI versions, current repository commit, explicitly
unknown original runtime commit, previous outcomes/retries, available aggregate
usage/cost/timing, quota diagnosis, and before-deletion file/byte counts.
It preserves minimal control/accounting evidence, not raw payload copies.

Pre-deletion file SHA-256 snapshots were saved in the session workspace.
All selected trial payload paths were individually resolved and validated as
direct children of the two known run directories, with no symlinks.
No Docker containers or tmux jobs were running.

## Changes

* Opus: permanently deleted nine already-pending quota trial directories,
  including their original agent payloads, earlier retry payloads, and renamed
  terminal markers: 369 files / 24,829,535 bytes.
* Sol: permanently deleted exactly 14 quota-interrupted trial directories,
  including passing partial patches and associated verifier/terminal payloads:
  401 files / 171,464,270 bytes.
* All other evidence, both `run-plan.json` files, and both append-only
  `run.jsonl` files remain byte-identical.
* Moved Sol's old 56-row export to
  [superseded analytics history](superseded-analytics/codex-gpt-6.1-sol-default-0.169.0-before-quota-reopen.jsonl).
  Its SHA-256 is unchanged. This is a superseded measurement snapshot, not
  a raw interrupted-execution archive.
* Rebuilt only the WharfSWE manifest and withdrew Sol from completed README
  comparisons: 56 completed arms / 3,136 trials. Retained costs: 3,080/3,136
  (98.21%); exception-bearing trials: five.
* Copilot-only SVG inputs did not change; all three graphs remain unchanged.
* Amended source decisions, PRD v1.1, readiness, and ledger to reflect the
  explicit Opus cleanup authorization. The future batch feature is not built.

## Read-only runtime checks

The inherited obsolete `WHARF_CLI_VERSION` initially blocked dry-run.
For these checks only, removed `WHARF_CLI_VERSION` and `CLI_VERSION` using
`env -u`; no shell/user environment settings were modified.

Current Release runtime's `resume --dry-run` now reports:

```text
Opus: completed=47 pending=9 K=4 N=56 P=12 cli=2.1.326
Sol:  completed=42 pending=14 K=4 N=56 P=12 cli=0.169.0
```

Every pending slot has `trial-retry=0` because its rejected raw retry payloads
were permanently deleted. Prior retry metadata is retained in the audit.
Opus retains 46 passes and one ordinary failed trial; Sol retains 40 passes
and two ordinary failed trials. Both still have 56 planned logical slots.

These are real recovery fixtures; automated implementation tests must use
sanitized synthetic equivalents rather than executing or mutating these jobs.
Do not run unbounded legacy resume to test batching: `run -b` is not implemented.
