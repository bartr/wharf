# WharfSWE analytics refresh review

## Accepted

The completed plan and recorded changes satisfy the requested analytics
refresh. Review is read-only; no implementation changes were needed.

* Five new completed arms exported through canonical commands.
* 280 new logical trials retained, including all reward-zero outcomes.
* Full 14-task/K=4 identities and checksums validated against retained plans.
* Rewards, test counts, costs, retry-aware agent timing, trial spans, patch
  bytes, and exception categories match retained evidence.
* Manifest has 57 arms / 3,192 trials. Cost coverage explicitly distinguishes
  3,080 measured Copilot rows from 112 unknown Codex rows.
* All 57 README results match and sort correctly. Latest Copilot P=16 and
  Codex P=12 configurations are distinguished; historical caveats remain.
* Three SVGs each contain 49 validated Copilot-only points; a focused
  mixed-provider regression confirms Codex cannot break revision plots.
* Historical tracked exports unchanged; no DeepSWE, raw job evidence,
  incomplete Claude recovery, or benchmark payload changes.

Execution completed and checks passed. No defects, decision gaps, or
follow-up implementation work identified. Changes remain uncommitted.
