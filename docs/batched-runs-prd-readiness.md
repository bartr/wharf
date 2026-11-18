# Quota-aware batched runs: specification readiness

Status: READY
Date: 2026-11-18
Source: [captured request and verified repository context](../.github/tracking/2026-11-18-batched-runs-notes.md)
Planned PRD: `docs/batched-runs-prd.md`
Planned LLM ledger: `docs/batched-runs-requirements.jsonl`

User decisions are recorded under Decisions in the source notes.
The original specification step changed no runtime or evidence. The later
fixture-preparation amendment separately authorized audited cleanup of 14 Sol
and nine Opus payloads plus withdrawal of the superseded Sol analytics snapshot.
Runtime implementation remains out of scope. The original gaps below are
retained for traceability and are resolved by the recorded decisions.

## 1. Source Inventory

| Source | Date | Authority | Notes |
|---|---|---|---|
| [Batch notes](../.github/tracking/2026-11-18-batched-runs-notes.md) | 2026-11-18 | Primary | Verbatim request, ten stated claims, read-only job/code findings |
| [Earlier Claude recovery record](../.github/tracking/2026-11-17-claude-usage-limit-resume.md) | 2026-11-17 | Supporting | Nine pending attempts; preserve original failures and retry evidence |

Earlier conversations and exported cost estimates are context, not permission
to change accounting or substitute estimates for missing billing evidence.

## 2. Coverage by PRD Section

| PRD section | Coverage | Evidence | Missing |
|---|---|---|---|
| 1. Overview / 1.1 deliberate choices | Partial | S-001/002/010 | A-010 |
| 2. Goals | Complete | S-001 through S-007 | None |
| 3. Non-goals | Partial | S-010 | Scope approval in A-001/004 |
| 4. Architecture | Partial | Verified run/resume path | Stop boundary and classification decisions |
| 5. Data | Partial | Retained plans/results | Archive, batch state, recovery semantics |
| 6. Interface | Partial | S-003/007/008 | Command, aliases, limits, exit codes |
| 7. Observability | Partial | S-004/005 | Batch summary and stop reason contract |
| 8. Deployment | Existing | Current CLI/Docker boundary | No new deployment proposed |
| 9. Build/packaging | Existing | Current CLI | No packaging changes proposed |
| 10. Tests/benchmarks | Partial | Proposed acceptance checks below | Confirm semantics before final criteria |
| 11. Configuration | Partial | S-006 | Batch-only overrides and legacy recovery |
| 12. Inner loop | Partial | S-007/010 | Approved command examples |
| 13. Security | Existing | Current credential boundaries | Refresh credentials without persisting secrets |
| 14. Acceptance criteria | Partial | Section 8 below | Blocked by A-001 through A-009 |
| 15. Open questions | Complete | Sections 3-7 | User decisions pending |
| 16. Provenance | Complete | Notes and cited context | Final ledger not yet generated |

## 3. Blocking Gaps

No unresolved blocking gaps. The original gaps below were resolved by the
grouped user response. A-005 was rejected in favor of permanent raw-payload
deletion with a minimal durable audit/control record. All other behavior was
confirmed, with exact exit code and storage/class details deliberately delegated.

| ID | Missing decision | Why it blocks | Proposed assumption | Add decision under |
|---|---|---|---|---|
| G-001 | Command surface and creation/continuation routing | Different commands have different compatibility risks | A-001: extend `run -b` using existing recovery | Notes / Decisions / Commands |
| G-002 | Batch unit and total completion target | `K`, total trials, admitted work, and successes differ | A-002: at most B logical trial admissions, finish task-count times K | Notes / Decisions / Counting |
| G-003 | In-flight work at exhaustion | Killing all work can discard valid paid computation | A-003: stop new agent starts, drain running work | Notes / Decisions / Exhaustion |
| G-004 | What counts as capacity exhaustion and when to stop | Generic 429s include transient throttling | A-004: explicit provider quota/reset signals, not every error | Notes / Decisions / Classification |
| G-005 | Literal deletion versus active-result removal | Irreversible evidence loss conflicts with audit/recovery | A-005: archive excluded executions and keep logical identities pending | Notes / Decisions / Evidence |
| G-006 | Historical Sol recovery and existing published analytics | Silently removing passing trials biases scores | A-006: reopen exactly 14, withdraw completed-arm listing until restored | Notes / Decisions / Migration |
| G-007 | Allowed configuration changes on continuation | Ambient environment or latest lookup can change the experiment | A-007: immutable run policy, new credentials only | Notes / Decisions / Configuration |
| G-008 | Missing usage without quota; ordinary failures | Rerunning normal test failures becomes pass hunting | A-008: normal failures stay terminal; missing usage alone does not reopen | Notes / Decisions / Terminal states |
| G-009 | Invocation outcomes, persistence, crash/lock behavior | Pausing must be distinguishable from completion/failure | A-009: explicit paused/completed state, durable batches, one writer | Notes / Decisions / Lifecycle |

## 4. Non-Blocking Gaps

No unresolved non-blocking gaps: internal design is deliberately delegated.

| ID | Missing decision | Consequence | Proposed assumption | Add decision under |
|---|---|---|---|---|
| G-101 | Internal class decomposition, test fixture arrangement | No user-visible effect | A-010: delegate within existing patterns | Notes / Decisions / Deliberate choices |

## 5. Assumption Resolution

### A-001: Command surface and scope (confirmed)

Use `wharf run -b B`, with long form `--batch-size B`, rather than a separate
`batch` command. It shares the existing run/resume implementation boundary.

```bash
# Create a new fixed K=4 plan; execute at most 25 pending logical trials.
wharf run --cli codex --model gpt-6.1-sol -k 4 -b 25

# Continue from the exact all-N directory; do not infer a nearby/latest run.
cd jobs/codex-gpt-6.1-sol-default-0.169.0/all-1
wharf run -b 1
wharf run -b 2

# Or target a run explicitly from anywhere.
wharf run --run-directory /absolute/path/to/all-1 -b 20
```

Without an explicit path, recognize only a valid plan in the current directory;
otherwise use normal new-run creation. An explicit missing/invalid run path
fails, rather than silently creating a different run. Without `-b`, preserve
existing `run` and `resume` command behavior; do not silently make an ordinary
`run` resume an existing directory.

Quota-stop behavior applies to Claude/Codex even without `-b`, so unbounded
legacy runs also stop wasting capacity. Bounded batching works for Copilot too.
Existing unbounded `resume` also inherits quota stopping, as explicitly approved.

### A-002: Counting and ordering (confirmed)

`B` is a positive integer maximum of distinct pending logical attempts admitted
by this invocation, not passes, tasks, concurrency, or additional K waves.
For 14 tasks at K=4, completion means 56 terminal logical trials, including
ordinary failed trials, not 56 successes or K growing between invocations.
`-b 1` can admit only one trial even when retained N=56.

Preserve existing attempt-wave and duration ordering across invocation
boundaries. B greater than remaining pending work executes only the remainder.
A quota-interrupted slot remains pending but is not replaced within that
invocation. Existing bounded infrastructure/verifier retries belong to the
same admitted slot, not extra B slots. A later invocation receives a new B
budget and may retry that same logical slot. B=0, negative, malformed, or
overflowing values fail before any mutation or resource preparation.

### A-003: Stop and drain (confirmed)

The first recognized quota signal closes a run-scoped agent-admission gate
as soon as the signal is observed, not after verification or whole-trial
retry. No new agent invocation, including an automatic retry, starts after
the stop is latched. Preserve evidence and clean up normally.

Let already running agents and verifiers finish. Accept other valid completed
results; invalidate any execution that independently reports exhaustion.
Work admitted but still preparing must not start an agent and remains pending.
Agents whose starts raced before the stop was latched count as already running;
the spec must not promise zero already-in-flight work with N-way concurrency.
Do not kill unrelated runs or introduce account-wide cross-run coordination.

### A-004: Exhaustion classification (confirmed)

Recognize explicit account/subscription credit exhaustion, usage-limit/reset
messages, and structured provider quota codes for Claude/Codex. Retain the
provider reason and reset timestamp when supplied, without guessing timezone.
Do not treat every HTTP 429, temporary service capacity issue, network failure,
model-catalog refresh error, or nonzero exit as a five-hour quota exhaustion.
Temporary service/rate failures keep their existing bounded retry policy.

No automatic sleeping until reset, sign-in, model switching, fallback provider,
or agent re-execution after quota. The operator starts the next batch manually.
Missing final usage does not by itself prove quota exhaustion.

### A-005: Evidence archival (rejected; replaced by permanent deletion)

"Delete" means permanently delete the interrupted execution payload, not move
raw logs or patches into an archive. Before deleting, persist a minimal,
secret-free audit record under `.github/tracking/` plus durable rejection and
pending-slot control state. Record identity, configuration, commit/version
evidence, outcome/error reason, available aggregate telemetry, known cost/time,
exact affected paths, and diagnosis; explicitly mark unknown measurements.
Do not delete unrelated trial history or the immutable run plan.

The logical attempt remains pending regardless of an already-passing partial
patch. The next batch starts with a pristine workspace and pinned CLI image,
not the interrupted agent session or partial patch. Do not write a valid
terminal result for the interrupted execution. No unaudited deletion, reset
of unrelated history, or success-shaped fallback when audit/deletion fails.

Preserve the complete planned task/K universe as the scoring denominator.
Track rejected executions, any known spend, and time separately from final
accepted-result metrics; never erase actual consumed resources. Exact archive
schema and separation between accepted cost and all-in spend are deliberately
delegated with these invariants.

### A-006: Historical migration (confirmed; raw archival replaced by deletion)

Later implementation includes an explicit, dry-runnable, idempotent migration:
audit, permanently delete interrupted payloads, and reopen exactly the
14 identified quota-interrupted Codex Sol slots,
even though their patches passed. Leave its 40 other passes and two test
failures terminal. Leave the existing nine Claude pending slots pending and
preserve their prior retry/cost/timing metadata in the minimal audit. The later
fixture amendment authorizes deleting their raw payloads too while preserving
the other 47 results and both original plans/logs. Do not blanket-delete
nonzero exits.

Archive the previously exported Codex Sol snapshot as superseded audit history
outside active `data/*.jsonl` discovery; remove that incomplete arm from the
completed-arm manifest/comparison until all 56 slots are terminal again.
If interim reporting is shown, use 56 planned trials, with 14 pending, not a
42-trial pass rate. Re-export and rebuild the WharfSWE manifest/documentation
after recovery finishes. Do not alter unrelated arms or DeepSWE.

### A-007: Frozen policy and credentials (confirmed)

Continuing batches restore the retained task subset/checksums, provider,
exact effective CLI version, model, effort, K, N, P, timeouts, retry policy,
ordering, and other run settings. No new latest-version resolution. Reject
conflicting flags; do not silently accept policy-changing ambient variables.
Batch size and diagnostic/read-only options can vary per invocation.

Credentials may be refreshed through existing provider credential locations;
never freeze or persist credential values. Revalidate auth before preparation.
Missing images may be rebuilt for the same pinned identity, not a newer CLI.
Reject missing/corrupt/mismatched retained metadata read-only; do not infer a
new experiment. Older supported runs can be migrated only explicitly.

### A-008: Terminal results and missing telemetry (confirmed)

Keep ordinary completed test failures, timeouts, and non-quota failures under
existing policy. Missing usage alone produces an explicit telemetry warning
and null measurements, not automatic reruns or exclusions. Only recognized
quota-interrupted executions are automatically invalidated for later batches.
This avoids selective retries until tests pass and avoids perpetual reruns
for providers that legitimately do not emit billing data.

### A-009: Lifecycle, dry-run, and writer ownership (confirmed; exact codes delegated)

Persist batch identity, requested B, selected/admitted logical IDs, admission
counts, accepted/pending counts, timestamps, stop reason, reset information,
and references to excluded executions. Never change task/K identities.
Statuses distinguish completed run, batch limit reached, quota pause, external
cancellation, and actual failure. Exit 0 for ordinary batch completion/no
remaining work; a distinct nonzero code for quota pause; existing error/
cancellation codes otherwise. The numeric quota-pause code is deliberately
delegated rather than fixed here.

A second writer targeting the same run fails before admission; read-only
status and dry-run remain available. Dry-run lists exact selected IDs and
remaining count without archive, state, directory, Docker, or network mutation.
After a crash, accepted results are not rerun and interrupted work remains
recoverable without duplicate active terminal results. Do not call a partially
executed K plan "completed" merely because one batch ended.

### A-010: Implementation latitude (deliberate)

Delegate internal class boundaries, helper naming, and test fixture placement
to implementation research, using existing mechanisms. Persisted schema shape
may also be delegated only if auditability, versioning, atomicity, crash
recovery, retry identity, and analytics separation invariants above are fixed.

## 6. Conflicts

All three conflicts are resolved. C-001: permanently delete raw payloads after
the durable audit prerequisite. C-002: manual invocations toward task-count
times K, not an automatic four/five-hour timer. C-003: explicit historical
recovery and withdrawal of the superseded completed-arm listing.
The table preserves the original conflicting positions for provenance.

| ID | Position A | Position B | Preferred source | Resolution needed |
|---|---|---|---|---|
| C-001 | Request says delete invalid agent runs | Existing recovery preserves audit evidence | New request defines validity; retain historical evidence | Confirm archive rather than irreversible deletion |
| C-002 | Request mentions five-hour and four-hour capacity; "completed k=n" | K means attempts per task; N means concurrency | Preserve literal user wording in notes | Confirm manual batches toward task-count times K, not timed windows or K=N |
| C-003 | Sol's 14 quota patches are currently counted as passes | New request rejects those attempts without token usage | New requested validity policy | Confirm explicit migration and withdrawal of superseded completed-arm snapshot |

## 7. Deliberately Unspecified Candidates

Both candidates below are now confirmed as deliberately unspecified.

| ID | Decision | Why implementation should own it | Confirmation needed |
|---|---|---|---|
| D-001 | Internal component/test layout | Match existing run/recovery helpers rather than prescribe classes | Confirmed |
| D-002 | Exact versioned batch/deletion-state schema and exit code | Needs implementation research; fixed invariants above | Confirmed |

## 8. Acceptance-Criteria Readiness

All semantic prerequisites are resolved. Final checkable criteria are in
`batched-runs-prd.md`, section 14.

| Goal or requirement | Measurable now | Missing measurement or threshold |
|---|---|---|
| 14 tasks/K=4; batches 25, 20, 1, 2, remainder | Yes after counting decision | At most B admitted slots each invocation; eventual 56 terminal slots |
| Stop at first quota signal | After A-003/004 | Deterministic concurrency test: no agent start after gate closes, including retries |
| Quota partial patch passes tests | After A-005 | Still pending; excluded patch cannot become accepted terminal result |
| Continue without repeating parameters | After A-001/007 | Pinned identity unchanged despite different ambient defaults/latest CLI |
| Directory and explicit path forms agree | After A-001 | Same selected pending IDs and run identity; no new run accidentally created |
| Drain in-flight work | After A-003 | Valid running results bank; quota results archive; prepared-only work stays pending |
| Ordinary failed tests do not rerun | After A-008 | Terminal failures excluded from later batch candidate set |
| Historical migration | After A-006 | Sol 42 terminal/14 pending; Claude 47 terminal/9 pending; durable audit precedes raw-payload deletion |
| Analytics cannot hide pending quota slots | After A-005/006 | No completed 42-trial Sol arm; planned denominator remains 56 |
| Crash, concurrency, archive failure | After A-009 | Single writer; read-only inspection; no lost evidence or duplicated accepted results |

## 9. LLM Artifact Recommendation

Recommend JSONL for the final requirements ledger because command, scheduling,
validity, migration, and accounting rules must remain atomic and traceable.
Use metadata, goal, requirement, constraint, decision, non_goal,
acceptance_criterion, deliberate, and open_question records.
Validate JSON parsing, unique stable IDs, valid cross-references, deterministic
ordering, and provenance against the same source claims as the human PRD.

Blocking decisions are resolved; generate both artifacts from confirmed notes.

## 10. Readiness Verdict

* Verdict: READY for final specification generation, not authorization to implement.
* Stated source requirements: 10, supplemented by nine confirmed decision groups.
* Unresolved blocking gaps: 0; unresolved non-blocking gaps: 0.
* Unresolved conflicts: 0 (three historical conflicts resolved).
* Assumptions awaiting confirmation: 0.
* Original assumptions: eight confirmed, one rejected/replaced, one deliberate.
* Deliberate choice groups confirmed: 2.
* Next action: generate and validate the human PRD and JSONL ledger, then stop
  for approval before implementation or historical mutation.

## Generated Artifact Validation

The final human PRD and JSONL ledger are generated. Validation passed for all
60 ledger records: unique IDs, deterministic ordering, source SHA-256 hashes
and headings, valid references, bidirectional requirement/acceptance links,
all 16 PRD sections, and local document links.

Record counts: metadata 1, goals 3, non-goals 5, constraints 3, decisions 3,
requirements 30, acceptance criteria 12, deliberate choices 3.
Labels: stated 57, deliberate 3, derived 0, open 0.
No proposed assumptions remain. At original generation, no runtime, job evidence, task, or analytics files
were changed. The separately authorized fixture cleanup and dependent analytics
withdrawal are recorded in
`../.github/tracking/2026-11-18-quota-recovery-fixtures-audit.json`.
No runtime source or pinned task files were modified.
