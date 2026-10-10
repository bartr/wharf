# Session Log

Intentional practice log. One entry per session.

Record start and end times when they occur. During close, cross-check them
against the first session commit and merge or tag timestamp.

---

## Session 20, 2026-10-07

Tag: `0.20.0`

### Close

* [x] Release owner named
* [x] Automated qualification matrix
* [x] Requirement-to-test coverage
* [x] Secret-scan hook
* [x] Release notes and operator documentation
* [x] Tests green

### Summary

Added repeatable release qualification, explicit ownership, package and
artifact checks, requirement coverage, and documented external infrastructure
gates. MVP remains deferred because real `smoke`, non-dry-run `run`, and
HydraFusion support are not yet implemented.

## Withdrawn Session 1, 2026-10-06

Start time: 15:44 CDT
End time: 16:05 CDT
Tag: 0.1.0 (withdrawn)

### Frame

Written before implementation.

* Goal, what does done look like: Ship a production-quality Wharf CLI foundation with stable command, configuration, logging, version, help, completion, exit-code, test, and build workflows.
* Out of scope, what I am explicitly not doing: Manifest parsing, Docker access, host checks, run-state creation, Harbor loading, Copilot execution, operational command implementations, push, merge, and tag creation.
* Failure condition, what would make this session a failure: A later command must bypass or replace the root command, configuration precedence, logger, version source, exit-code boundary, or validation workflow established here.

### During

* Parking lot, threads I wanted to pull but did not: Manifest parsing, `--dry-run`, Docker integration, and durable run-state behavior remain with their planned later sessions.
* Fit check decision, proceed. The plan fits one focused implementation pass because the packages are small, independent foundations with one shared validation command. No required 0.1.0 proof was cut; manifest and runtime behavior remain deferred as framed.

### Close

* [x] Tests green
* [x] Fast-forward merge
* [x] Tagged and pushed
* [x] Repo memory updated
* [x] `docs/RETRO.md` bullet appended
* [x] Next session starter written

### Summary

Built and released the initial Go implementation, then withdrew its `0.1.0`
tag when the canonical implementation decision changed to .NET. The Go
commits remain in history for recovery, but the release arc restarts at
Session 1 with the .NET 10 foundation.

### Next session starter

Complete and validate the .NET 10 CLI foundation before recreating `0.1.0`.

### Health signal

* Framing quality, 1 to 5: 5
* Drift, yes or no: no
* Close complete, yes or no: yes

---

## Session 9, 2026-10-06

Start time: 22:13 CDT
End time: 22:16 CDT
Tag: `0.9.0`

### Frame

* Goal, what does done look like: Execute one prepared task in an isolated
  agent environment and retain its complete trajectory and patch.
* Out of scope, what I am explicitly not doing: Verifier execution, patch
  application, and multi-task scheduling.
* Failure condition, what would make this session a failure: The agent sees
  hidden verifier material or an empty patch is treated as success.

### During

* Parking lot: verifier execution and pristine patch application belong to
  Session 10.
* Fit check decision, proceed. The runner boundary permits fake-agent tests
  without weakening the Docker isolation contract.

### Close

* [x] Tests green
* [x] Fast-forward merge
* [x] Tagged and pushed
* [x] Repo memory updated
* [x] Research, plan, changes, and review artifacts written

### Summary

Added isolated agent execution contracts and a Docker runner with explicit
mounts, network, and unprivileged UID. Durable evidence includes logs,
trajectory, patch, timing, usage, and result status; empty patches are
explicitly unfinished.

### Next session starter

Apply the retained patch to a pristine verifier and complete the single-task
vertical slice.

---

## Session 10, 2026-10-06

Start time: 22:16 CDT
End time: 22:19 CDT
Tag: not released (manual testing required)

### Frame

* Goal, what does done look like: Verify one retained agent patch in a
  separate pristine environment and persist a trustworthy result.
* Out of scope, what I am explicitly not doing: Concurrent datasets, resume,
  scoring aggregates, polished reports, and release approval.
* Failure condition, what would make this session a failure: Verification
  reuses the agent workspace or exposes verifier-only material.

### During

* Parking lot: scheduler and recovery work remain for Sessions 11-13.
* Fit check decision, proceed. The verifier boundary is injectable and
  manually testable without releasing the branch.

### Close

* [x] Tests green
* [ ] Fast-forward merge
* [ ] Tagged and pushed
* [x] Repo memory updated
* [x] Research, plan, changes, and review artifacts written

### Summary

Added pristine workspace creation, patch application, sequential verifier
commands with timeouts, raw evidence retention, and atomic trial results.
The `0.10.0` branch remains unmerged and untagged for manual testing.

### Next session starter

After manual approval, release `0.10.0`; then expand the proven one-task path
into a deterministic work-conserving scheduler.

---

## Session 11, 2026-10-06

Start time: 22:22 CDT
End time: 22:25 CDT
Tag: `0.11.0`

### Frame

* Goal, what does done look like: Schedule selected task attempts
  deterministically with an `N` trial limit and observable lifecycle states.
* Out of scope, what I am explicitly not doing: Infrastructure retry policy,
  phase-specific resources, and restart recovery.
* Failure condition, what would make this session a failure: Admission order
  changes results or successful work is not banked immediately.

### During

* Parking lot: resource limits, timeout multipliers, retry classification, and
  recovery remain Sessions 12 and 13.
* Fit check decision, proceed. The scheduler owns trial admission and logical
  attempt banking without weakening the image-preparation `B` boundary.

### Close

* [x] Tests green
* [x] Fast-forward merge
* [x] Tagged and pushed
* [x] Repo memory updated
* [x] Research, plan, changes, and review artifacts written

### Summary

Added deterministic, work-conserving scheduling with concurrency and trial
limits, successful logical-attempt banking, lifecycle events, and cancellation
handling.

### Next session starter

Add phase-specific resources, timeouts, and infrastructure retry semantics.

---

## Session 12, 2026-10-06

Start time: 22:32 CDT
End time: 22:35 CDT
Tag: `0.12.0`

### Frame

* Goal, what does done look like: Enforce phase-specific resources, timeout
  policies, and infrastructure retry semantics.
* Out of scope, what I am explicitly not doing: Process restart and operator
  stop cleanup.
* Failure condition, what would make this session a failure: An infrastructure
  retry changes logical-attempt accounting or exceeds its bound.

### During

* Parking lot: interruption recovery and resume remain Session 13.
* Fit check decision, proceed. Independent phase gates and separate retry
  evidence preserve the scheduler's logical-attempt contract.

### Close

* [x] Tests green
* [x] Fast-forward merge
* [x] Tagged and pushed
* [x] Repo memory updated
* [x] Research, plan, changes, and review artifacts written

### Summary

Added independent phase resource gates, timeout multipliers and overrides,
bounded infrastructure retries, and separate retry evidence without changing
logical-attempt identity.

### Next session starter

Recover an interrupted run from retained events without repeating completed
work.

---

## Session 13, 2026-10-06

Start time: 22:40 CDT
End time: 22:43 CDT
Tag: `0.13.0`

### Frame

* Goal, what does done look like: Reconstruct state and resume only unfinished
  or explicitly retryable work.
* Out of scope, what I am explicitly not doing: Operator stop cleanup,
  notifications, and reports.
* Failure condition, what would make this session a failure: Completed attempts
  rerun, retry counts change, or partial state is mistaken for success.

### During

* Parking lot: active-run status, safe stop, and notifications remain Session
  14.
* Fit check decision, proceed. Recovery planning is event-driven and reports
  orphan resources without mutating them.

### Close

* [x] Tests green
* [x] Fast-forward merge
* [x] Tagged and pushed
* [x] Repo memory updated
* [x] Research, plan, changes, and review artifacts written

### Summary

Added validated event replay, deterministic recovery planning, terminal versus
retryable classification, immutable retry accounting, and live-resource orphan
reporting.

### Next session starter

Make active runs observable and safely stoppable without deleting evidence.

---

## Session 14, 2026-10-06

Start time: 22:56 CDT
End time: 22:59 CDT
Tag: `0.14.0`

### Frame

* Goal, what does done look like: Provide non-mutating status, safe stop,
  retained evidence, and durable completion/blocked notifications.
* Out of scope, what I am explicitly not doing: Score computation and final
  report styling.
* Failure condition, what would make this session a failure: Status mutates
  state or stop removes retained evidence.

### During

* Parking lot: audit, scoring, and report generation remain Sessions 15 and
  16.
* Fit check decision, proceed. Cleanup accepts only explicitly ephemeral
  resource IDs and status remains a pure projection.

### Close

* [x] Tests green
* [x] Fast-forward merge
* [x] Tagged and pushed
* [x] Repo memory updated
* [x] Research, plan, changes, and review artifacts written

### Summary

Added read-only status projections, bounded graceful stop, targeted
ephemeral-resource cleanup, and durable completion/blocked notifications with
optional HTTPS webhooks.

### Next session starter

Audit retained evidence and compute PASS@1 and Enterprise Score from fixtures.

---

## Session 8, 2026-10-06

Start time: 22:11 CDT
End time: 22:14 CDT
Tag: `0.8.0`

### Frame

* Goal, what does done look like: Pin and validate Copilot CLI metadata and
  construct a secret-safe headless invocation.
* Out of scope, what I am explicitly not doing: Full task execution, agent
  containers, verifier execution, and patch capture.
* Failure condition, what would make this session a failure: Requested and
  effective versions differ silently or a synthetic credential appears in
  invocation data.

### During

* Parking lot: CLI installation and isolated execution belong to Session 9.
* Fit check decision, proceed. The adapter boundary is injectable and
  validates the secret and endpoint constraints before execution exists.

### Close

* [x] Tests green
* [x] Fast-forward merge
* [x] Tagged and pushed
* [x] Repo memory updated
* [x] Research, plan, changes, and review artifacts written

### Summary

Added typed Copilot version, effort, endpoint, credential-source, and
invocation contracts with exact version matching and fake-CLI tests. The
adapter supports environment, file, and stdin credential sources without
retaining credential values.

### Next session starter

Run Copilot in an isolated agent container and retain its complete trajectory
and patch.

---

## Session 2, 2026-10-06

Start time: 19:01 CDT
End time: 19:28 CDT
Tag: 0.2.0

### Frame

* Goal, what does done look like: Add reproducible typed run configuration,
  strict validation, redaction, and side-effect-free dry-run behavior.
* Out of scope, what I am explicitly not doing: Docker, host checks, durable
  run state, task discovery, scheduling, agent execution, verifier execution,
  and reporting.
* Failure condition, what would make this session a failure: Configuration is
  not reproducible or a secret or side effect escapes validation and dry-run.

### During

* Parking lot, threads I wanted to pull but did not: Docker and runtime
  execution remain with later sessions.
* Fit check decision, proceed. The configuration model, resolver, validator,
  renderer, and focused tests fit the session without expanding into runtime
  orchestration.

### Close

* [x] Tests green
* [x] Fast-forward merge
* [x] Tagged and pushed
* [x] Repo memory updated
* [x] `docs/RETRO.md` bullet appended
* [x] Next session starter written

### Summary

Added strict versioned manifests, explicit layered configuration precedence,
redacted deterministic rendering, and an offline dry-run path. The next
session begins durable event and summary contracts for validated runs.

### Next session starter

Create durable append-only event and summary contracts with replay and
idempotency guarantees.

### Health signal

* Framing quality, 1 to 5: 5
* Drift, yes or no: no
* Close complete, yes or no: yes

---

## Session N, YYYY-MM-DD

Start time:
End time:
Tag:

### Frame

Written before any other work.

* Goal, what does done look like:
* Out of scope, what I am explicitly not doing:
* Failure condition, what would make this session a failure:

### During

* Parking lot, threads I wanted to pull but did not:
* Fit check decision, proceed, cut, or re-frame, and what was cut:

### Close

* [ ] Tests green
* [ ] Fast-forward merge
* [ ] Tagged and pushed
* [ ] Repo memory updated
* [ ] `docs/RETRO.md` bullet appended
* [ ] Next session starter written

### Summary

What I built, what I decided, and what matters next time.

### Next session starter

One sentence naming where the next session begins.

### Health signal

* Framing quality, 1 to 5:
* Drift, yes or no:
* Close complete, yes or no:

---

## Session 3, 2026-10-06

Start time: 19:45 CDT
End time: 20:05 CDT
Tag: 0.3.0

### Frame

* Goal, what does done look like: Establish durable filesystem-backed state
  contracts with append-only events, replay, atomic summaries, and idempotent
  recovery.
* Out of scope, what I am explicitly not doing: Harbor loading, Docker and
  host checks, scheduling, execution, reporting, and operator commands.
* Failure condition, what would make this session a failure: State can be
  corrupted, duplicated, partially written, or replayed into a different
  result without an explicit error.

### During

* Parking lot, threads I wanted to pull but did not: Runtime lifecycle
  integration and later operational features remain deferred.
* Fit check decision, proceed. The filesystem contract, replay logic, atomic
  summary replacement, and focused tests fit the approved session boundary.

### Close

* [x] Tests green
* [x] Fast-forward merge
* [x] Tagged and pushed
* [x] Repo memory updated
* [x] `docs/RETRO.md` bullet appended
* [x] Next session starter written

### Summary

Added validated versioned state contracts, JSONL persistence, deterministic
replay, idempotent duplicate handling, atomic summaries, and concurrent reader
proofs. The next session begins Harbor task loading and checksum validation.

### Next session starter

Start Session 4 by loading and validating Harbor task definitions without
coupling state persistence to Docker or execution.

### Health signal

* Framing quality, 1 to 5: 5
* Drift, yes or no: no
* Close complete, yes or no: yes

---

## Session 4, 2026-10-06

Start time: 19:40 CDT
End time: 20:15 CDT
Tag: 0.4.0

### Frame

* Goal, what does done look like: Discover, validate, checksum, filter, and
  deterministically order Harbor-compatible DeepSWE tasks.
* Out of scope, what I am explicitly not doing: Container creation, host
  checks, scheduling, task execution, and reporting.
* Failure condition, what would make this session a failure: Equivalent task
  roots produce different task identities, checksums, order, or samples.

### During

* Parking lot, threads I wanted to pull but did not: Runtime dataset
  packaging, Docker, and execution remain deferred.
* Fit check decision, proceed. A small sanitized fixture set was selected
  instead of vendoring the full benchmark corpus or reference material.

### Close

* [x] Tests green
* [ ] Fast-forward merge
* [ ] Tagged and pushed
* [x] Repo memory updated
* [x] `docs/RETRO.md` bullet appended
* [x] Next session starter written

### Summary

Added schema-checked Harbor loading, canonical checksums, deterministic
selection, seeded sampling, sharding, and K-attempt expansion. Vendored
sanitized parser fixtures without solutions or hidden verifier data.

### Next session starter

Start Session 5 by implementing read-only supported-host and Docker preflight
checks without mutating the host.

## Session 15, 2026-10-06

### Close

* [x] Typed audit evidence and completeness states
* [x] PASS@1 and task-equal Enterprise Score
* [x] Cost, usage, coverage, regression, and runtime aggregation
* [x] Focused scoring fixtures

### Summary

Added deterministic scoring over expected and observed tasks. Missing, invalid,
partial, and zero measurements remain visible and cannot produce success-shaped
results. Enterprise scoring follows the documented 80/20 coverage/pass policy
and task-equal weighting.

### Next session starter

Render deterministic static and machine reports from audited retained state.

### Health signal

* Framing quality, 1 to 5: 5
* Drift, yes or no: no
* Close complete, yes or no: yes

---

## Session 5, 2026-10-06

Start time: 19:44 CDT
End time:
Tag: not yet tagged

### Frame

* Goal, what does done look like: Make `wharf check` classify supported hosts,
  Docker readiness, and capacity without mutating the host.
* Out of scope, what I am explicitly not doing: Docker configuration changes,
  image pulls, container creation, and release merge/tagging.
* Failure condition, what would make this session a failure: An unsafe host
  passes silently or an unsupported-host override hides a Docker/capacity
  failure.

### During

* Parking lot, threads I wanted to pull but did not: Network allocation and
  required image checks remain with image preparation sessions.
* Fit check decision, proceed. The probe boundary and CLI fit the session;
  release closeout is intentionally deferred for user inspection.

### Close

* [x] Tests green
* [ ] Fast-forward merge
* [ ] Tagged and pushed
* [x] Repo memory updated
* [x] `docs/RETRO.md` bullet appended
* [x] Next session starter written

### Summary

Added injectable platform, Docker, and capacity probes plus read-only
`wharf check` output. This branch remains unmerged and untagged by request.

### Next session starter

Review Session 5, then either release `0.5.0` or adjust preflight evidence
before continuing to image pulls.

### Health signal

* Framing quality, 1 to 5: 5
* Drift, yes or no: no
* Close complete, yes or no: pending user review

---

## Session 6, 2026-10-06

Start time: 20:11 CDT
End time: 20:18 CDT
Tag: `0.6.0`

### Frame

* Goal, what does done look like: Pull required images once with bounded,
  jittered, decaying 429 retries and complete durable telemetry.
* Out of scope, what I am explicitly not doing: Derived image builds, trial
  containers, the complete `B` gate, and release merge/tagging.
* Failure condition, what would make this session a failure: A 429 is absent
  from durable evidence or a deterministic registry failure is retried as a
  rate limit.

### During

* Parking lot, threads I wanted to pull but did not: Session 7 must place base
  pulls, context preparation, and derived builds behind one `B` gate.
  Address-pool/VPN analysis, ephemeral network allocation, and required
  local-only base-image policy remain preparation inputs where applicable.
* Fit check decision, proceed. The coordinator and evidence boundary deliver
  the Session 6 outcome without inventing a public pull semaphore or starting
  Session 7 derived-build work.

### Close

* [x] Tests green
* [x] Fast-forward merge
* [x] Tagged and pushed
* [x] Repo memory updated
* [x] `docs/RETRO.md` bullet appended
* [x] Next session starter written

### Summary

Added local-first, duplicate-coalesced Docker image pulls with strict registry
rate-limit classification, capped jittered backoff, pressure decay, durable
per-429 JSONL, atomic summaries, and aggregate/verbose console reporting.
The release was fast-forwarded to `main`, tagged `0.6.0`, and followed by the
upstream `0.7.0` branch.

### Next session starter

Review Session 6, then extend image preparation through the full `B`-gated
derived-build pipeline without adding a separate pull semaphore.

---

## Session 7, 2026-10-06

Start time: 22:03 CDT
End time: 22:06 CDT
Tag: `0.7.0` (pending release close)

### Frame

* Goal, what does done look like: Prepare reusable derived agent images behind
  one complete `B` concurrency gate.
* Out of scope, what I am explicitly not doing: Copilot execution, verifier
  containers, and multi-task scheduling.
* Failure condition, what would make this session a failure: Base pulls escape
  the gate or identical builds run twice.

### During

* Parking lot: Docker command wiring and trial-container execution remain for
  later sessions behind the backend boundary.
* Fit check decision, proceed. The coordinator owns the complete gate and
  deterministic cache lifecycle without adding a second pull semaphore.

### Close

* [x] Tests green
* [ ] Fast-forward merge
* [ ] Tagged and pushed
* [x] Repo memory updated
* [x] Research, plan, changes, and review artifacts written

### Summary

Added deterministic derived-image cache keys and a reusable coordinator with
one `B` gate across base preparation, context preparation, and image builds.
Normal requests coalesce and reuse successful images; force builds bypass the
cache, and failed or caller-cancelled work does not poison later requests.

### Next session starter

Pin, validate, and invoke Copilot CLI without leaking credentials.

### Health signal

* Framing quality, 1 to 5: 5
* Drift, yes or no: no
* Close complete, yes or no: yes

---

## Mid-arc reflection, after session 5

* Where did framing break down?
* Where did drift happen, and why?
* Was the close ritual ever skipped, and what did that cost the next session?
* What changes for the remaining sessions?

---

## Arc 1 checkpoint, after session 10

* Is the single-task execution slice complete and trustworthy?
* Which decisions must carry into the operations arc?
* Which parking-lot items should be scheduled, rejected, or deferred?
* Does the remaining ten-session frame still fit?

---

## Arc reflection, after session 20

* Framing quality trend across the arc:
* Drift frequency and pattern:
* Close ritual completion rate:
* Did intentional sessions compound faster than accidental ones?
* What would the next arc look like with this discipline in place?

---

## N=32 qualification checkpoint, 2026-10-08

### Frame

* Goal: qualify the real 113-task Copilot run at K=1, N=32, and B=12 on z01,
  compare clean matched outcomes with Pier, and convert every observed
  reliability defect into retained, tested behavior.
* Out of scope: claiming a final score before the active run completes or
  committing benchmark solutions and verifier-only material.
* Failure condition: trust success-shaped command exits, lose patches from
  failed agents, allow known OOM loops to block the run, or compare unlike
  timing boundaries as if they were equivalent.

### Summary

The qualification exposed pnpm recursion, 4 GiB cgroup OOMs, misleading
progress accounting, AI Credit unit conversion, leading-dash prompt parsing,
a transient Copilot HTTP 400, missing agent/build timeout enforcement, reward
zero misclassification, and incomplete patch capture. Wharf now uses 8 GiB
agent/verifier limits for future runs, stops on Docker OOM events, enforces all
four task timeouts, retries the explicit transient Copilot failure once,
captures complete patches, and verifies every nonempty patch regardless of
agent exit status.

Manual Koota verification proved the failed-agent policy: the PID-exhausted
agent was stopped with exit 137, but its complete preserved patch passed 43/43
F2P and 172/172 P2P tests for reward 1.

### Validation

* Focused agent, verifier, and loader tests: 26/26 passed.
* Build: passed.
* Manual Koota verifier: reward 1.
* Full suite: deferred until the active N=32 run releases Docker capacity.

### Next session starter

Finish and rescore the active run from retained evidence, then repeat
K=1/N=32/B=12 with the corrected binary.

---

## Session 22 close: `0.22.0`, 2026-10-08

### Frame

* Goal: harden and qualify Wharf for reliable K=1/N=32 benchmark execution,
  preserving complete evidence through infrastructure failures and drain-tail
  behavior.
* Out of scope: changing the public preparation-concurrency contract or
  claiming final results for the newly started `all-3` run.
* Failure condition: lose retained patches, permit unbounded container resource
  failure, or tag behavior that had not passed focused regression and isolated
  Koota qualification.
* Start: qualification continued from the earlier checkpoint; an exact session
  start time was not recorded.
* End: 2026-10-08 13:28 CDT.

### Summary

`0.22.0` adds structured stage progress, corrected occupancy and K grouping,
combined agent-plus-verifier scheduling estimates, Docker default swap
allowance under the retained 8 GiB memory limit, retryable verifier timeout and
external termination, and a separate retained agent home mounted outside the
project workspace. The home change fixes the pnpm self-install recursion that
exhausted Koota's PID limit. An isolated Koota K=1/N=1/B=1 run completed in
18 minutes 9 seconds with no trial retry and passed 100% F2P and P2P.

The full `all-3` K=1/N=32/B=12 run started with the `0.22.0` binary and remains
qualification evidence in progress; it is not treated as a completed release
gate.

### Close

* Focused external-home tests: 28/28 passed.
* Isolated Koota verifier: reward 1, F2P 100%, P2P 100%.
* Full unit suite: 147/147 passed.
* Framing quality: 4/5.
* Drift: yes; live-run failures expanded the qualification hardening scope.
* Close complete: pending merge and tag at the time of this entry.

### Next session starter

Start `0.23.0` by replacing public `B` build concurrency with `P` preparation
concurrency. Apply one shared preparation gate to agent image/workspace/Copilot
setup and verifier image/workspace/patch setup, while releasing the permit
before verifier commands execute. Add occupancy and wait-time telemetry before
using the cold Docker run to tune P.

---

## Session 23: shared preparation capacity, 2026-10-08

### Frame

* Goal: release the first `0.23.0` change with a breaking B-to-P configuration
  rename, one observable preparation gate shared by agents and verifiers, and
  no permit held during verifier execution.
* Out of scope: compatibility aliases, changing the default concurrency,
  gating verifier commands, tuning z01 before cold-run evidence, or changing
  the interactive progress layout.
* Failure condition: an old B surface remains active, verifier execution holds
  a preparation permit, or telemetry cannot reconstruct occupancy and wait
  time.
* Start: 2026-10-08 13:31 CDT.

### Fit check

Proceed. The plan fits if the releaseable cut remains configuration rename,
shared gate boundary, durable telemetry, tests, and normative documentation;
interactive progress visualization, default tuning, and the cold benchmark
remain out of scope.

### Controlled K=2 benchmark decision, 2026-10-08 15:36 CDT

Before starting another benchmark or seeing its outcome, exclude All-1 from
comparative scoring. It predates the finalized 8 GiB memory policy, Docker OOM
handling, bounded trial retry, complete patch capture, failed-agent
verification, and reliable cost propagation. Retain the complete All-1
directory unchanged as auditable pre-contract qualification evidence rather
than deleting it.

All-2 remains transitional qualification evidence because it crossed the
hardening period, and All-3 remains the first clean released `0.22.0` K=1
result. Neither will be combined with the new run and presented as one
homogeneous K cohort. The next primary result is a fresh z01 run on the pushed
`0.23.0` code with K=2, N=32, and P=12 across the fixed 113-task suite.

The result is accepted regardless of outcome. Primary pass rate is passing
trials divided by 226. Primary cost is USD per task attempt, never aggregate
run cost; report cost coverage separately and label any optional imputation.
All OOMs, timeouts, infrastructure retries, trial retries, and verifier
outcomes remain in the result. Record the source commit, effective Wharf and
Copilot versions, manifest, task checksum, and warm/cold Docker state before
interpreting the run.

### Qualification enhancement list

* Report terminal attempt completion independently from grader-result
  coverage. A K wave whose 113 attempts all reached a terminal state must show
  `complete=113/113`, even when only 112 or 111 attempts produced test
  statistics. Show missing grader results separately and classify their
  terminal outcomes instead of leaving them looking active.
* Track queue entry, stage start, and queue duration for every staged
  operation. Include agent and verifier image builds, workspace preparation,
  patch application, and agent and verifier cleanup so P-gate pressure can be
  separated from build, cleanup, and execution time.
* Add stage-level queue counts and wait-time aggregates to durable status and
  reporting. Include operation type and K wave so qualification analysis can
  identify where work is waiting as the run drains.
* Add a `queued=<count>` field to interactive Wharf progress. Keep it distinct
  from active occupancy and completed counts, and derive it from structured
  queue transitions rather than inferred process state.
