# Codex quota recognition follow-up

## Research and defect

The Astra `wharf-smoke` attempt in Codex CLI 0.169.0 emitted structured `error`
and `turn.failed` messages containing `You\u2019ve hit your usage limit`.
`AgentQuotaParser.FindValue` matched only an ASCII apostrophe. Consequently,
Wharf classified subscription exhaustion as `AgentExecution`, ran one
whole-trial retry, and retained an erroneous terminal `agent-failed` result.
Neither execution produced a final usage/cost summary.

The provider also supplied `Nov 19th, 2026 2:53 AM` without a timezone; the
clock-only reset parser did not preserve that date. No timezone is inferred.

## Bounded corrective plan

Reuse the approved quota contract and existing parser/observer/execution tests:

1. Normalize the right apostrophe only in parsed error-message matching.
2. Recognize and validate the explicit dated reset format, preserving a
   timezone-free date/time rather than inventing an offset.
3. Cover error/turn envelopes, Claude compatibility, invalid calendar dates,
   live stdout/stderr notification and no-retry execution with a partial patch.
4. Validate without model calls. Leave retained Astra evidence untouched until
   the operator approves repair of its incorrectly terminal slot.

## Implementation

Changes are limited to the provider quota parser, focused tests and directly
related documentation. Raw provider streams remain unchanged. Generic rate
limits and non-error envelopes retain their prior classification rules.

## Validation and review

* Test-project build passed with zero warnings/errors.
* 105/105 focused quota/execution/batch/retry tests passed in named tmux session
  `wharf-quota-unicode-20261118`, including live stdout/stderr notification and
  the real dated error format. No model calls were made.
* Touched source/test formatting verification and `git diff --check` passed.
* Astra's batch state and failed terminal result were not modified:
  * `batches.json` SHA-256:
    `45028c6c6012f9c1b040bd46e0c4b3eedbdefb53ff1571ac8241eff43f6d8ce1`
  * Failed `wharf-smoke` root `result.json` SHA-256:
    `318c3a796993e8e59f033591b0e50c9b1adbc4c872283d5419d8162114e77d48`
## Approved installation and repair

The operator approved both installation and permanent audit-before-delete
repair through the confirmation form.

* Published Release output and installed its complete 20-file payload into
  `/home/bartr/bin`, using per-file atomic replacement rather than truncating
  any potentially mapped DLL. No provider CLI or credential file was changed.
* Installed `wharf --version` reports `1.6.0`. Installed and staged `wharf.dll`
  SHA-256 both equal
  `a5f74e3e9b8b71c01266a0874cadcf132cf107e16a5daa1fa92a120ed42daa9d`.
* A one-off repair helper acquired the same exclusive `FileStream` writer lock
  as RunBatch, verified the approved batch/result hashes, and verified explicit
  quota messages in both failed executions.
* Dry-run preceded mutation. The helper wrote the durable
  [audit](quota-astra-smoke-20261118.json), atomically appended a rejection
  tombstone, permanently deleted only `wharf-smoke__64b432f7__attempt-1`
  (including its quota-failed retry), then finalized the deletion marker.
* Preserved 13,080 ms of actual agent-command time. Token/cost and unexecuted
  functional/regression-test measurements remain explicitly null.
* All other run files, including all 11 passing payloads, the immutable plan,
  and append-only run log were verified byte-identical by SHA-256 before/after.
* Installed `wharf progress` now reports 11 completed, 11 passing, zero errors,
  zero active work. Historical retry count remains visible in the append-only
  log; it is not an extra accepted logical trial.
* Installed batch dry-run reports 45 pending and selects `wharf-smoke` attempt 1
  next, pinned to Codex CLI 0.169.0. No agents were started.
* A second repair invocation was a verified no-op.

The source fix, regression tests, documentation and repair audit remain
uncommitted.

## Operator-initiated live quota validation

The operator then ran another `-b 1` from the correct Astra directory while
quota was exhausted. Read-only inspection verified:

* Invocation `9b6c3fe9b8f54b31bed2e9b9afa3a918` finished `quota-paused`.
* One slot was admitted/started, none accepted; the historical retry count
  did not increase.
* The structured quota reason and timezone-free reset `2026-11-19 02:53`
  were retained.
* Durable runtime audit `quota-6e419d3933884df4bf47e0de6197337c.json` exists;
  its deletion marker is complete and retains 3,566 ms of agent time.
* Progress remains 11 completed, 11 passing, zero errors or running work.
* Batch dry-run still reports 45 pending and selects the same `wharf-smoke`
  attempt next. No diagnostic command started another agent.
