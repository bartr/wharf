# Quota-Aware Batched Runs Product Requirements

Status: Specification v1.1; recovery fixtures prepared; runtime implementation not started
Date: 2026-11-18
Owner: Repository maintainer
Sources: [request and confirmed decisions](../.github/tracking/2026-11-18-batched-runs-notes.md)
Readiness: [READY](batched-runs-prd-readiness.md)
Machine companion: [requirements ledger](batched-runs-requirements.jsonl)

## 1. Overview

Wharf will execute a fixed benchmark plan over multiple manually initiated
batches, allowing subscription-authenticated Claude Code and Codex runs to
continue after quota resets. A batch limits new logical-trial admissions,
not concurrency, passes, or attempts per task. Explicit quota exhaustion
stops new agents, drains already running work, rejects interrupted executions
even when their partial patches pass, and leaves their logical slots pending.

The user confirmed the command, counting, stop, migration, configuration, and
telemetry decisions. The user explicitly rejected raw-evidence archival in
favor of permanent deletion of interrupted execution payloads.
The later fixture-preparation instruction authorizes the documented historical
payload cleanup, but not runtime implementation or launching agents.

### 1.1 Deliberately Unspecified

| Fixed here | Left to implementation research |
|---|---|
| Commands, frozen policy, slot counting, stop/drain behavior, validity, migration identities | Internal component names and test layout |
| Atomicity, versioned state, single writer, audit-before-delete, crash recovery, stable logical identity | Exact versioned batch/deletion-state schema and filenames |
| Exit 0 for ordinary batch completion/no work; distinct nonzero quota-pause exit | Exact quota-pause numeric exit code, compatible with existing CLI codes |
| Known rejected spend/time remains observable separately from accepted results | Internal representation of accepted versus all-in aggregates |

These are deliberate choices, not unresolved public behavior. Implementation
research must document the selected contracts before building.

## 2. Goals

* **GOAL-001:** Finish one fixed task-count times K experiment across arbitrarily
  sized invocations without repeating parameters or duplicating accepted slots.
* **GOAL-002:** Avoid further agent calls after explicit quota exhaustion without
  cancelling already running valid work.
* **GOAL-003:** Preserve reproducibility and honest measurement while reopening
  quota-interrupted logical trials, including the identified historical runs.

## 3. Non-Goals

* **NG-001:** Automatic waiting/scheduling until a quota reset or inferring an
  exact four/five-hour allowance. The operator decides when to invoke a batch.
* **NG-002:** Automatic sign-in, model/provider fallback, or account-wide quota
  coordination across unrelated runs.
* **NG-003:** Increasing K/N or retrying ordinary failed tests until they pass.
* **NG-004:** Replacing missing billing evidence with API estimates or missing
  token counts with guesses.
* **NG-005:** Changing smoke/prebuild into batch commands, changing benchmark
  task payloads, or altering DeepSWE history as part of this feature.

## 4. Architecture

### 4.1 Components

| Boundary | Purpose | Existing context |
|---|---|---|
| Command routing | Create bounded new run or continue exact retained run | `CommandTree` run/resume |
| Retained planning | Restore fixed task universe and configuration | `RetainedRunResumePlanner` |
| Admission and quota latch | Bound B slots, prevent starts after stop | Actual `RunService` parallel execution path |
| Provider classification | Distinguish quota exhaustion from generic failure | Claude/Codex structured events and diagnostics |
| Execution finalization | Bank valid results or audit/delete rejected payload | Existing atomic results and cleanup boundaries |
| Batch state and analytics | Persist progress, rejection, and measurement provenance | Retained plans/logs and canonical exports |

The current production path uses `Parallel.ForEachAsync` in `RunService`,
not the standalone `DeterministicScheduler`. Implementation must govern
the actual path and reuse recovery rather than create competing execution logic.

### 4.2 Shape

Command -> retained/new plan -> deterministic pending-slot selection ->
bounded admission -> preparation -> agent -> ordinary verifier/finalization.
Provider quota recognition closes agent admission immediately. Valid work
already running drains; quota-interrupted work enters audit/delete/pending
recovery instead of being accepted. Durable batch state summarizes both.

## 5. Data

### 5.1 Source and format

Use the retained `run-plan.json`, task IDs/checksums, run events, terminal
results, provider streams, and available aggregate telemetry. Batch state is
versioned and filesystem-backed. Do not retain credential values.

### 5.2 Packaging

Batch continuation targets the existing all-N run directory, not a new job or
new K universe. State must be discoverable there without repeating parameters.

### 5.3 Loading

Validate retained metadata/task identities before mutation. Derive candidates
from pending logical slots; ordinary terminal results are not candidates.
No directory-name-only reconstruction of unsupported/malformed run policy.

### 5.4 Seed data

Use synthetic provider quota events and sanitized test fixtures. Historical
evidence below is read-only research input until explicit migration execution
is authorized. Tests must not consume real subscription credits.

### 5.5 Schema

The exact schema is deliberately delegated subject to these requirements:

* **REQ-001:** Keep task selection, checksums, K, and logical-attempt identities
  unchanged across batches; completion means task-count times K terminal slots.
* **REQ-002:** Persist a versioned batch record with identity, requested B,
  selected/admitted slot IDs and counts, timestamps, accepted/pending counts,
  stop reason, provider reset information when supplied, and rejection references.
* **REQ-003:** Persist rejection/pending control state and a minimal durable
  secret-free audit record under `.github/tracking/` before permanently deleting
  a quota-interrupted execution payload. Record run configuration K/N/P,
  provider/model/effort/CLI version and available commit identity, logical slot,
  outcome/error reason, available aggregate telemetry/known cost/time, affected
  paths, and diagnosis; explicitly mark missing fields unknown.
* **REQ-004:** Permanently delete only the quota-interrupted execution payload:
  raw logs, trajectory, partial patch, workspace and any associated verifier
  payload/accepted terminal marker. Do not retain raw copies under archives
  or retry paths; do not delete the immutable plan, batch/audit control records,
  unrelated slots, or unrelated execution history.
* **REQ-005:** Make finalization and audit-before-delete crash-safe and
  idempotent. Audit/deletion failures surface explicitly and block successful
  invalidation; interrupted cleanup remains recoverable without accepting a
  quota patch, duplicating terminal results, or losing logical identity.
* **REQ-006:** Keep known consumed cost/time from rejected executions observable
  separately from accepted-result metrics. Missing measurements remain null;
  no rejected raw execution or retry becomes an additional logical trial.

## 6. Interface Surface

| Command | Behavior |
|---|---|
| `wharf run <initial settings> -b B` | Create one fixed plan and admit up to B pending slots |
| `wharf run -b B` in an exact valid run directory | Continue that run using retained policy |
| `wharf run --run-directory PATH -b B` | Continue the explicitly named retained run |
| `wharf run --batch-size B` | Long alias for `-b B` |
| `wharf run -b B --dry-run` with either directory form | Read-only exact batch plan |
| Existing `wharf run` / `wharf resume` forms | Preserve routing; inherit approved Claude/Codex quota stopping |

* **REQ-007:** Add `-b B` / `--batch-size B` to `run` and reuse existing
  run/recovery behavior instead of introducing a separate `batch` command.
  Batching also supports Copilot.
* **REQ-008:** An explicit run directory selects that exact run. Without an
  explicit path, `run -b` continues only a valid retained run in the current
  directory; otherwise it follows ordinary new-run creation. Never search for
  a nearby/latest run. Recognizable corrupt run metadata fails rather than
  silently creating a new experiment.
* **REQ-009:** Without `-b`, preserve existing ordinary `run`/`resume` routing
  and supported overrides; in particular, ordinary `run` does not silently
  resume based on its working directory. Quota stopping still applies as
  specified in REQ-017.
* **REQ-010:** B limits distinct pending logical attempts admitted by the
  invocation, not successful outcomes, unique tasks, K waves, or concurrency.
  Distinct logical slots starting agents cannot exceed B even when retained N
  is larger than B; ordinary retries remain inside their admitted slot.
* **REQ-011:** Preserve existing attempt-wave/duration ordering across batch
  boundaries. Run only the remaining candidates when B exceeds pending work.
  Do not admit replacement slots for quota-interrupted work in the same batch.
* **REQ-012:** Existing bounded infrastructure/verifier retries belong to their
  admitted logical slot and consume no extra B slot. Quota-interrupted slots
  wait for a later invocation with a new B budget, not automatic same-batch retry.

### 6.1 Validation rules

* **REQ-013:** Require positive integer B; reject zero, negative, malformed and
  overflowing values before state/output-directory/resource mutation. Reject
  explicit missing/invalid run paths, corrupt or inconsistent policy, task
  checksum mismatches, and conflicting continuation flags explicitly.
* **REQ-014:** A completed run admits no work and exits successfully. A
  partially completed plan is not a completed run merely because its batch ends.
* **REQ-015:** Dry-run reports exact selected logical IDs, admitted budget and
  remaining count with no archive/deletion/state/output-directory/Docker/network
  mutation. Validate both current-directory and explicit-path forms.

## 7. Observability

### 7.1 Metrics

* **REQ-016:** Distinguish completed run, batch limit reached, quota pause,
  external cancellation, and actual failure. Report requested/admitted/started/
  accepted/pending work and rejected quota executions distinctly. Normal batch
  completion and no remaining work exit 0; quota pause has a distinct nonzero
  code selected during implementation research.

### 7.2 Logging

* **REQ-017:** For Claude/Codex, the first observed explicit account/credit/
  usage-limit exhaustion or reset signal closes run-scoped agent admission.
  Apply this to new runs and resumes with or without `-b`; retain a secret-free
  reason and provider reset information without guessing timezone.
* **REQ-018:** No new agent invocation or automatic agent retry starts after
  the quota latch closes, including work still in preparation. Such work stays
  pending. Starts already committed before the latch are in-flight work.
* **REQ-019:** Drain already running agents/verifiers, retain their valid
  results, and reject each execution that independently reports quota. Do not
  cancel unrelated runs or invalidate other valid work because quota was hit.
* **REQ-020:** Explicit quota exhaustion is not every HTTP 429, model-catalog
  refresh failure, network error, generic nonzero exit, or transient service
  capacity error. Those retain existing bounded failure/retry behavior.

### 7.3 Dashboards

No separate dashboard deployment. Existing console/status surfaces must expose
the run and batch state consistently; final analytics follow section 10.

## 8. Deployment

Existing supported CLI/Docker environment and tmux operational guidance apply.
No daemon, account quota polling service, or new external service is required.

* **REQ-021:** Enforce one writer per retained run; concurrent batch/resume
  writers fail before admission while read-only status and dry-run remain
  available. Crash recovery preserves accepted results and recovers unfinished
  work without duplicate accepted terminal markers.

## 9. Build and Packaging

Use the existing build/package mechanism; no new dependencies or package
format are required by the specification. This task delivers documents only,
not release packages or a new installed binary.

## 10. Testing and Benchmarks

### 10.1 Unit tests

Test B bounds/counting, deterministic selection, provider classification,
frozen settings, terminal filtering, and state transitions with synthetic data.

* **REQ-022:** A quota-interrupted execution is invalid even if its partial
  patch passed verification. Permanently delete its payload after the durable
  audit prerequisite, leave the same slot pending, and restart from a pristine
  workspace with the pinned CLI in a later batch.
* **REQ-023:** Missing token/billing usage without recognized quota is a
  warning/null measurement, not an automatic reopen. Ordinary test failures
  and non-quota terminal outcomes keep existing policy; do not selectively
  rerun them until passing.

### 10.2 Integration tests

Test run/continuation/recovery with B below N, retries inside a slot, quota
while other work prepares/runs/verifies, and failure injection during deletion.

* **REQ-024:** Keep the entire planned task-count times K universe in scoring.
  Pending slots do not shrink the denominator; excluded executions/retries
  do not add trials. Never present an incomplete 42-slot snapshot as a
  completed 56-slot run or substitute API estimates for retained billed costs.

### 10.3 End-to-end tests

* **REQ-025:** Provide an explicit, dry-runnable, idempotent historical
  migration for Codex Sol 0.169.0 `all-1`. Audit and permanently delete/reopen
  exactly the 14 quota-interrupted slots listed below. Keep its other 40
  passes and two test failures terminal: 42 terminal, 14 pending of 56.
* **REQ-026:** Keep Claude Code 2.1.326 `all-1` at 47 terminal/nine pending,
  but permanently delete the nine pending quota payloads after the durable
  audit prerequisite, as explicitly authorized in the fixture amendment.
  Keep its other 47 terminal slots, immutable plan and append-only log unchanged.
  Preserve prior retry/cost/timing metadata in the audit, not raw payload copies.
  Do not blanket-delete nonzero exits or mutate unrelated arms/DeepSWE.
* **REQ-027:** During Sol migration, preserve its superseded exported analytics
  snapshot as audit history outside active `data/*.jsonl` discovery and
  withdraw that incomplete arm from the completed manifest/README comparison.
  After all 56 slots are terminal, re-export canonically and rebuild WharfSWE
  manifest/documentation. This snapshot retention does not authorize keeping
  rejected raw agent payloads.

Migration target:
`wharf-swe/jobs/codex-gpt-6.1-sol-default-0.169.0/all-1`.
Affected task attempts: agent 2/3/4; audit 1/2; host-profile 1; image-pull 1/4;
reporting 2; scheduler 3; smoke 4; verifier 1/2/3.
The source notes retain full task IDs and provenance.

Claude target:
`wharf-swe/jobs/claude-claude-opus-5-5-default-2.1.326/all-1`.
This feature may continue its nine already-pending slots with pristine
execution payloads; it does not reclassify the other 47 during migration.

### 10.4 Performance targets

No speculative throughput or millisecond SLA. The measurable safety bound is
at most B logical admissions and zero new agent starts after the stop latch,
tested with deterministic barriers around the admission/start boundary.
Already running work may continue consuming quota until it drains.

## 11. Configuration

| Setting | Initial run | Continuing batch |
|---|---|---|
| Batch size | Required positive B when bounded | Fresh invocation-local B |
| Provider/model/effort/K/N/P and other run policy | Existing configuration precedence | Retained, immutable |
| CLI version | Existing resolution, retain exact effective version | Exact retained version; never resolve latest |
| Task subset/checksums/order | Selected and retained | Validated unchanged |
| Credentials | Existing provider source | Refreshed through existing source, never stored in plan |
| Diagnostic/read-only options | Existing controls | May vary |

* **REQ-028:** Continuing batches restore task subset/checksums, provider,
  exact effective CLI version, model, effort, K/N/P, ordering, timeouts,
  retries, and other run settings. Only B and diagnostic/read-only controls
  vary; reject conflicting overrides and do not let ambient policy defaults
  replace retained values. Do not resolve a newer CLI.
* **REQ-029:** Read refreshed credentials through existing provider locations
  and validate before resource preparation without persisting values. Rebuild
  missing images only for the same pinned CLI identity.
* **REQ-030:** Unsupported legacy, malformed, or mismatched retained metadata
  fails read-only unless explicitly migrated; batch continuation must not
  guess a new policy. Preserve existing non-batch resume compatibility.

## 12. Inner-Loop Dev Process

1. Approve this specification before implementation.
2. Research delegated schema/exit code and the actual run/recovery boundaries.
3. Implement and validate using focused existing tests and synthetic providers.
4. Document initial and both continuation command forms below in the README.
5. Historical fixture cleanup was separately authorized and performed. Any
   later migration or recovery execution still needs its own authorization;
   do not launch paid benchmark agents as an incidental test.

```bash
# Example: resolve the CLI once and retain it with the fixed K=4 plan.
wharf run --cli codex --model gpt-6.1-sol -k 4 -b 25

# Supply the exact emitted all-N directory; no repeated model/version settings.
wharf run --run-directory /absolute/path/to/all-1 -b 20
cd /absolute/path/to/all-1
wharf run -b 1
wharf run -b 2
```

Changing B does not change retained concurrency. Operators can run additional
batches immediately while quota remains, or manually wait for reset.

## 13. Security

Existing credential, container ownership, network, and evidence-redaction
boundaries remain unchanged. The quota detector must not print credentials.
Deletion is limited to resolved payload paths owned by the selected execution;
no broad wildcard/container cleanup or deletion of a run/workspace root.
Deletion records are secret-free; do not copy raw user/agent content into them.
Cleanup and audit failures must surface rather than masquerade as success.

## 14. Acceptance Criteria

* **AC-001:** For 14 tasks/K=4, consecutive budgets 25, 20, 1, 2, and 25
  without interruptions admit 25, 20, 1, 2, and 8 slots, producing exactly
  56 unique terminal logical results. Test with N larger than B and mixed
  passing/ordinary-failing outcomes. A sixth invocation admits zero and exits 0.
* **AC-002:** Invalid B, explicit invalid run paths, conflicting flags, corrupt
  metadata and task checksum mismatches fail before mutation. Current-directory
  and explicit-path continuations choose the same pending IDs; no ancestor or
  latest-run discovery occurs. Legacy ordinary run/resume routing stays intact.
* **AC-003:** With prepared, agent-running and verifier-running slots, inject a
  recognized quota event. No new agent start/retry occurs after latch closure;
  existing work drains, valid results bank, prepared-only slots remain pending,
  and the invocation reports quota pause with a distinct nonzero exit.
* **AC-004:** Claude/Codex explicit quota signals trigger AC-003 both with and
  without B, including resume. Generic transient 429/network/catalog/capacity
  errors and missing-usage-only cases do not trigger quota invalidation.
* **AC-005:** A quota patch that passes tests is still rejected. Verify the
  durable secret-free audit/control record exists before payload deletion,
  rejected raw payloads are absent, other history is unchanged, and the same
  slot later uses a pristine workspace and exact pinned CLI.
* **AC-006:** Inject crashes/failures before and during deletion/finalization.
  Recovery neither loses the audit nor accepts a quota patch or duplicates a
  terminal result. Failures are explicit. A competing writer cannot admit work;
  status and dry-run remain available without mutation or network activity.
* **AC-007:** Change ambient model/CLI/concurrency defaults and refresh auth
  between batches. Continuation keeps every retained policy value, does not
  resolve latest, accepts refreshed credentials and diagnostic/B changes,
  rejects policy overrides, and never persists secret values.
* **AC-008:** Normal infrastructure/verifier retries consume one admitted slot;
  no same-batch quota replacement occurs. A later batch selects the pending
  slot in preserved order. Ordinary failed tests stay terminal.
* **AC-009:** Migration dry-run selects exactly the documented 14 Sol slots;
  execution yields 42 terminal/14 pending, keeps Claude at 47/9 while removing
  its nine explicitly authorized pending payloads after audit, preserves both
  plans/logs and all unrelated results, and a repeated migration is a no-op.
* **AC-010:** Sol migration withdraws its completed-arm listing while preserving
  the superseded export outside active discovery. Interim reporting keeps
  denominator 56, not 42. Completion permits canonical 56-row re-export.
  Rejected known spend/time remains separately visible; nulls stay null.
* **AC-011:** Batch records reconstruct requested/admitted/started/accepted/
  pending work and distinguish complete, limit, quota, cancellation and failure.
  No batch limit is reported as completion of the full unfinished run.
* **AC-012:** Existing build/type checks and focused run/resume/provider/
  analytics tests pass. No tests mutate pinned tasks, real retained evidence,
  or consume paid model quota.

## 15. Open Questions

No unresolved behavioral questions. Schema layout, class/test decomposition,
exact numeric quota exit code, and aggregate representation are deliberately
delegated in section 1.1, with fixed invariants and required research evidence.

The 23 selected historical payload directories were deleted under the later
fixture-preparation authorization. Runtime implementation and running agents
remain unauthorized in this task.

## 16. Provenance

| Source | Date | Contributed |
|---|---|---|
| [Captured request / S-001 through S-010](../.github/tracking/2026-11-18-batched-runs-notes.md) | 2026-11-18 | Motivation, command examples, frozen identity, quota retry, spec-first scope |
| Same notes / Decisions / Commands, Counting | 2026-11-18 | `run -b`, 56-slot target, B semantics, compatibility |
| Same notes / Decisions / Exhaustion, Classification | 2026-11-18 | Stop/drain boundary, explicit quota scope, no automatic waiting |
| Same notes / Decisions / Evidence, Migration | 2026-11-18 | Permanent raw deletion, audit prerequisite, exact historical recovery, analytics withdrawal |
| Same notes / Decisions / Configuration, Terminal states | 2026-11-18 | Frozen policy, refreshed auth, ordinary failures and missing telemetry |
| Same notes / Decisions / Lifecycle | 2026-11-18 | Durable batches, single writer, crash safety, deliberate implementation latitude |
| Same notes / Decisions / Recovery fixture preparation amendment | 2026-11-18 | Authorizes preparing both real runs, including deletion of nine already-pending Opus payloads |
| [Existing Claude recovery record](../.github/tracking/2026-11-17-claude-usage-limit-resume.md) | 2026-11-17 | Nine already-pending slots and retained retry history |
