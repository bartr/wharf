# Quota-aware batches: implementation evidence

## Changes

* `run -b` / `--batch-size` limits pending logical admissions without changing
  the fixed K universe or ordinary terminal-failure accounting.
* Exact-CWD and `--run-directory` continuation restore pinned policy before
  consulting ambient run settings; explicit policy overrides fail.
* Initial/retained batch dry-runs are read-only; invalid B fails before output
  creation. Quota pause exits 6; normal batch/no-work completion exits 0.
* Schema 1.0 `batches.json` records selections, admissions, committed starts,
  accepted/pending counts, timestamps, status, quota/reset details and rejection
  references. An exclusive run writer covers replan through finalization.
* Explicit Claude/Codex subscription signals close the run-local start gate
  during streaming. Prepared work and retries cannot start afterward; active
  valid work drains. Generic 429/service overload retains prior behavior.
* Live rejection tombstones and secret-free tracking audits precede deletion.
  Final/recovered audits refresh known aggregate usage/cost/time; unavailable
  values remain null. Audits validate run/trial identity before deletion.
* Quota payloads and terminal markers are removed, not archived. Unrelated
  execution histories and retry budgets/cost survive. Read-only recovery and
  analytics never accept a pending-deletion result.
* Accepted analytics use the accepted invocation's stage evidence, preventing
  rejected/historical quota errors and reset delays contaminating a later row.
  Progress reconciles logical identity across batches and excludes rejected slots.
* Added synthetic runtime/state/CLI/recovery/concurrency/cost/cancellation tests.
  A preparation-invocation injection avoids credentials/network in runtime tests.
* README documents start, continuation, dry-run, quota handling and exit behavior.

## Validation completed during implementation

* Provider boundary: 114/114 focused tests (delegated implementation).
* Early integrated provider/run/recovery/analytics selection: 137/137 tests.
* After progress/baseline fixes: 64/64 focused tests.
* Debug test-project build and Release application build: zero warnings/errors.
* Opus explicit-path dry-run: 9 pending, CLI 2.1.326, one selected.
* Sol exact-CWD dry-run: 14 pending, CLI 0.169.0, two selected.
* Both retained dry-runs ignored deliberately conflicting ambient CLI/model/
  obsolete version settings, without resolving a new CLI version.
* Both original fixture plans/logs retained their pre-validation SHA-256 values.
* Final offline regression suite: 522/522 passed in named tmux session
  `wharf-batches-20261118-qualified`; the live-model integration test was
  deliberately filtered out. Runtime batch tests include cancellation and
  malformed logical-identity coverage.
* Final source/test whitespace-format verification and `git diff --check`
  passed. See the review for commands and acceptance evidence.

## Validation exception

The existing command-discovery test unexpectedly executes a live model with
local credentials. Its exact agent container was stopped; that test is excluded
from final offline qualification. See
[the incident record](2026-11-18-batched-runs-validation-incident.md).
No Opus/Sol recovery run or benchmark export was executed or modified.
