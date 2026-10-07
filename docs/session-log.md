# Session Log

Intentional practice log. One entry per session.

Record start and end times when they occur. During close, cross-check them
against the first session commit and merge or tag timestamp.

---

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
