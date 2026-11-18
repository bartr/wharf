# Quota-aware batched runs: source notes

Date: 2026-11-18
Status: Specification decisions confirmed. No runtime changes authorized in this task.

## User request

> look at the wharf-swe/jobs folder - there should be a claude code job that didn't finish. There should also be a codex sol 6.1 job that got several out of capacity errors - while most of those passed, they should be deleted as we didn't get token information from the job.  Now that we are using claude and codex, we need to add the capability to run a k=4 test in "batches" (-b 25 as an example). Once the 5 hour limit resets, we should be able to run another batch and so on until we've completed k=n. If an agent gets an out of capacity error, no more agents should be scheduled for that run and that agent run should be deleted so it can be retried in the next "batch". If I ran a -b 20 as an example and determined I had credit left, I should be able to run a -b 1 or -b 2 or -b n to "use up" all of the 4 hour capacity. Everything else should be the same (espcially CLI version) and I shouldn't have to specify the params. I should be able to run "wharf run -b 1" from the directory of the run or by specifying the run directory. I'm not sure if "batch" should be a separate command or if we should use "run" and alter the behavior based on the presence of -b. let's create a spec and answer any questions before implementing

## Stated requirements

* S-001: Support incremental batches for a K=4 benchmark.
* S-002: A later invocation can run another batch after quota resets.
* S-003: Batch sizes can vary per invocation, including 25, 20, 1, 2, or n.
* S-004: Capacity exhaustion stops further scheduling for that run.
* S-005: Capacity-interrupted agent executions are removed from accepted
  results, even if a partial patch passed tests, and retried in a later batch.
* S-006: Continuing batches preserves run parameters, especially CLI version,
  without requiring the user to repeat them.
* S-007: Support the run directory as the working directory or an explicit path.
* S-008: Resolve whether to extend `run` or add `batch` before implementation.
* S-009: Reopen the affected historical Codex Sol executions with missing token
  information. Exact cleanup/archive semantics are unresolved.
* S-010: Produce a specification and resolve questions before implementation.

## Observed jobs (read-only inventory)

* Claude: `wharf-swe/jobs/claude-claude-opus-5-5-default-2.1.326/all-1`.
  Plan: 56 logical trials. Active terminal markers: 47 (46 passed, 1 failed).
  Nine quota terminal markers were already renamed, not deleted, as documented
  in `2026-11-17-claude-usage-limit-resume.md`. Those nine remain pending.
* Codex: `wharf-swe/jobs/codex-gpt-6.1-sol-default-0.169.0/all-1`.
  Plan: 56 logical trials. Active terminal results: 54 passed, 2 failed.
  Exactly 14 original agent executions exit 1 with explicit "You've hit your
  usage limit" trajectory events and missing final usage summaries.
  All 14 retained partial patches passed verification.
  Reopening exactly those 14 would leave 40 passes + 2 test failures terminal
  and 14 pending, not 42 trials total.
* The Codex Sol arm is already in `wharf-swe/data/` and its manifest/README.
  Historical recovery must address that published snapshot without silently
  dropping the 14 logical trials or claiming a completed 42-trial run.

Quota-affected Codex Sol task attempts:

* wharf-agent: 2, 3, 4
* wharf-audit: 1, 2
* wharf-host-profile: 1
* wharf-image-pull: 1, 4
* wharf-reporting: 2
* wharf-scheduler: 3
* wharf-smoke: 4
* wharf-verifier: 1, 2, 3

## Verified implementation context

* `src/wharf/Command/CommandTree.cs:430-530`: a separate `resume` command
  already restores retained configuration and supports explicit run paths,
  dry-run, and N/P overrides. It currently requires `--run-directory`.
* `src/wharf/Recovery/RetainedRunResumePlanner.cs:92-166`: retained task subset,
  checksums, provider/model/effort/version, and plan/log consistency are checked.
* `src/wharf/Recovery/RetainedRunResumePlanner.cs:171-194`: valid root
  `result.json` marks an attempt terminal, regardless of earlier agent failure.
* `src/wharf/Run/RunService.cs:166-221`: the actual run path uses
  `Parallel.ForEachAsync`, not `DeterministicScheduler`; resume evidence is
  archived before new execution. Batch admission must govern this actual path.
* `src/wharf/Run/RunService.cs:437-470`: failed agent status becomes a generic
  `AgentExecution` category when no typed error exists.
* `src/wharf/Run/RunService.cs:757-867`: terminal markers are atomically written;
  whole-trial retry policy excludes some typed errors but not provider quota.
* `src/wharf/Agent/CodexUsageParser.cs:45-64`: error/turn.failed becomes a
  failure bit; no completed turn means usage remains null.
* `src/wharf/Agent/ClaudeUsageParser.cs:63-80`: result `is_error` becomes a
  failure bit; no quota-specific classification is exposed here.

## Decisions

The user answered the grouped specification form on 2026-11-18:

### Commands

`command=Extend run -b as recommended`.
Accept A-001 in the readiness review: `run -b` / `--batch-size`, current
exact run directory or explicit `--run-directory`, no accidental discovery of
nearby/latest runs, unchanged ordinary `run` routing without `-b`, existing
`resume` command retained. Batching also works for Copilot.

### Counting

`counting=Count admitted logical attempts; finish task-count × K`.
Accept A-002: positive B bounds admitted pending logical slots, not successes,
tasks, concurrency, or K growth. Normal retries stay inside a slot; quota
interruptions receive no replacement in the same invocation. Preserve order,
run only the remainder when B is larger, and reject invalid B read-only.
Five-hour/four-hour wording describes manually managed subscription windows,
not an automatic timer. For 14 tasks/K=4 the target is 56 terminal trials.

### Exhaustion

`inflight=Stop new starts; drain already running work`.
Accept A-003: latch stop on the first recognized signal, including blocking new
automatic agent retries and prepared-but-not-started agent work. Drain agents
and verifiers already running, bank valid results, and reopen every execution
that independently reports quota. Do not stop unrelated runs.

### Classification

`capacity=Explicit quota exhaustion; apply to runs/resumes even without -b`.
Accept A-004: Claude/Codex explicit quota/credit/reset errors stop admission on
bounded and unbounded runs and resumes. Generic transient HTTP 429, network,
model-catalog, and service-capacity errors retain their existing policy.
No automatic waiting, authentication, fallback provider, or model switching.

### Evidence

`evidence=Permanently delete interrupted execution evidence`.
Reject A-005's raw-evidence archival alternative. Delete quota-interrupted
execution payloads permanently, including partial patches and verifier
payloads, rather than banking them or keeping raw copies under retry/archive
directories. The same logical attempt becomes pending for a pristine retry
on the next batch with the pinned CLI. Passing partial patches do not exempt
an execution. This does not authorize deleting unrelated execution history.

Repository audit prerequisite: before deleting any failed benchmark payload,
durably record secret-free run configuration (K/N/P, versions and commit
evidence), trial identity, outcome/error reason, available aggregate telemetry,
known cost/time, affected paths, and diagnosis under `.github/tracking/`.
Batch control state records the rejection and retry eligibility atomically.
This minimal deletion/control record is not a retained raw execution archive.
Record unknown fields explicitly; never fabricate missing costs or tokens.
Keep consumed resources visible separately from accepted-result metrics.
The scoring universe and logical identity remain unchanged.

### Migration

`migration=Include this explicit historical migration in scope`.
Accept A-006 with permanent raw-payload deletion replacing raw archival:
explicit dry-runnable/idempotent migration reopens exactly 14 Codex Sol quota
slots, leaves the other 42 terminal results, and preserves Claude's existing
47-terminal/9-pending state. Include superseded exported Sol snapshot archival
outside active discovery, withdraw its completed-arm listing until recovery
finishes, and keep the planned denominator 56. This archival approval concerns
the already-published analytics snapshot, not raw interrupted agent payloads.
No history changes until implementation is separately authorized.

### Configuration

`configuration=Freeze run policy; allow new batch size and refreshed credentials`.
Accept A-007: restore all retained settings, especially exact effective CLI
version and task checksums; reject conflicting overrides and ignore ambient
policy defaults in continuation. Only B, diagnostic/read-only controls and
fresh credentials vary. No latest lookup. Validate auth before preparation.
Missing pinned images may be rebuilt without changing their CLI version.
Unsupported legacy/corrupt/mismatched metadata fails read-only unless explicitly
migrated. Existing ordinary resume compatibility must not be silently changed.

### Terminal states

`telemetry=Only quota-interrupted attempts automatically reopen`.
Accept A-008: missing usage alone produces a warning/null telemetry, not
automatic retries. Normal terminal outcomes, including ordinary failed tests,
follow existing policy and are not selectively rerun until passing.

### Lifecycle

`lifecycle=Approve lifecycle invariants and delegate internal details`.
Accept A-009/010: durable batch summaries, distinct completed/limit/quota/
cancelled/failed states, single writer per run, side-effect-free dry-run,
crash-safe recovery, no duplicate accepted terminal results.
Exit 0 for normal batch completion/no work; a distinct nonzero code for a
quota pause. Exact numeric exit code, versioned storage layout, and internal
class/test structure are deliberately delegated to implementation research.
Preserve audit, versioning, atomicity, crash recovery, retry identity, and
accepted-cost/all-in-spend separation invariants.

### Recovery fixture preparation amendment

The later user instruction is: "before we modify the code, let's change the
opus and sol runs to reflect the new behavior so that we can use those as tests".
When asked whether to remove the already-pending Opus payloads as well, the
user explicitly selected: "Delete the nine pending Opus payloads too".

This authorizes operational preparation now, not runtime implementation.
It supersedes the earlier requirement to preserve the nine pending Claude raw
payloads: permanently delete those nine payloads after the audit prerequisite,
while keeping its 47 other terminal slots unchanged. Preserve prior
retry/cost/timing information in the minimal audit, not the deleted raw payload.
Codex still reopens exactly 14 interrupted slots and keeps 42 terminal.
Keep both immutable plans and append-only run logs byte-identical.

The existing runtime's read-only resume planner should see Opus 47/9 and Sol
42/14, each deleted pending slot with a fresh execution payload and no
retained raw retry directories. These are real operational recovery fixtures,
not automatic test inputs that may be executed or mutated without authorization.
Future automated tests use sanitized synthetic equivalents.
The audit and execution record is `2026-11-18-quota-recovery-fixtures-audit.json`.
