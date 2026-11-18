# Quota-aware batches: review

## Verdict

Accepted against the approved v1.1 PRD and implementation plan, with one
explicit pre-existing live-integration-test exclusion. Runtime changes are
uncommitted and have not replaced the installed Wharf binary.

## Executed qualification

* `dotnet build tests/wharf.Tests/wharf.Tests.csproj --no-restore -v quiet`:
  passed, zero warnings/errors.
* `dotnet build src/wharf/wharf.csproj -c Release --no-restore -v quiet`:
  passed, zero warnings/errors.
* Final regression command, run in foreground in named tmux session
  `wharf-batches-20261118-qualified`:

  ```bash
  env -u WHARF_CLI_VERSION -u CLI_VERSION \
    dotnet test tests/wharf.Tests/wharf.Tests.csproj \
    --no-build --no-restore \
    --filter 'FullyQualifiedName!~WharfAppTests.RunCommandExecutesTaskDiscovery' \
    -v minimal
  ```

  **522 passed, zero failed/skipped; exit 0.** The one live-model test is
  excluded, not counted as passed. Obsolete environment variables are removed
  only from this validation process, not from user settings.
* Touched source/test files passed `dotnet format whitespace` verification.
  `git diff --check` passed.

## Requirement checks

* Fixed-universe accounting: 14 synthetic tasks at K=4 complete through
  budgets 25/20/1/2/25 as exactly 25/20/1/2/8 admissions. Ordinary grading
  failures remain terminal, with no pass hunting. Selected IDs preserve the
  existing attempt-wave/duration ordering.
* B below retained N, invalid B, completed-run no-op, frozen task subset,
  explicit continuation policy rejection, and exclusive writer/read-only
  planning are exercised without credentials or network.
* Explicit quota closes the shared start gate. Concurrent valid work drains;
  no replacement starts. A later invocation retries only pending logical slots.
* Live tombstones exist before an agent returns. Interrupted deletion replays
  idempotently; missing/mismatched audits block deletion. Known USD and credits
  remain distinct, secret values are absent, and legitimate prior retry
  budgets/cost/payloads survive.
* Rejected payloads are absent, not archived; root terminal markers are not
  accepted while deletion is pending. Analytics exclude rejected stage errors
  and progress excludes rejected terminal/active counts. External cancellation
  is recorded as cancelled, not completed.
* Provider classification/live-streaming tests cover explicit subscription
  exhaustion versus generic rate limits/transient errors.
* The qualified Release CLI dry-runs both real fixtures, with deliberately
  conflicting ambient CLI/model/obsolete-version settings:
  * Opus: 9 pending, CLI 2.1.326, one selected.
  * Sol exact-CWD form: 14 pending, CLI 0.169.0, two selected.
  Neither fixture was executed. Original plans/logs retain their original
  SHA-256 values; no benchmark tasks or active exports were edited.

## Follow-up outside this implementation

`WharfAppTests.RunCommandExecutesTaskDiscovery` is not an offline discovery
test: it can launch a paid model using local credentials. A targeted rerun
encountered this, and its exact live container was stopped. Move that existing
test behind explicit opt-in or replace its runtime dependencies in a separate
test-isolation change. See the
[incident record](2026-11-18-batched-runs-validation-incident.md) for known facts,
missing cost telemetry, and the test's automatic evidence cleanup.

No unresolved batching defect was identified in the final qualification.
