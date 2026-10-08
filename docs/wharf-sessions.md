# Wharf MVP implementation sessions

Status: MVP deferred pending real `smoke`, `run`, and HydraFusion support
Source: `docs/wharf.jsonl`
Source SHA-256: `3648a03025ceafedb604b0a712918866e4f85e6e3d2a137f76aa3ba0a96482d2`
Documentation layout: requirements and session records are under `docs/`;
Research, Plan, Changes, and Review artifacts are under `.github/tracking/`.
Session model: one very senior engineer paired with a frontier model
Target duration: 60-90 focused minutes per session

## Planning rules

- Each session runs one Research, Plan, Implement, Review loop.
- Research and Plan must leave durable artifacts under `.github/tracking/`.
- The fit check may cut scope but may not remove the session's releaseable outcome.
- New work discovered during a session goes in the parking lot.
- Every session ends with green tests, a fast-forward merge, a dot-release tag,
  updated repo memory, a session-log entry, and a `docs/RETRO.md` bullet.
- Long-running integration tests and benchmarks run in a named `tmux` session
  according to `RUN_MONITORING.md`.
- A tag is not cut when the session's listed proof is missing.
- The plan assumes Wharf is implemented in this repository. Session 1 must
  settle and pin the .NET SDK before scaffolding the application.
- Post-v1 repair/replay requirements `REQ-153` through `REQ-180` and `AC-019`
  are excluded from this MVP arc.
- Post-MVP work includes the Pier-style derived agent-image cache: install the
  requested Copilot version into local task images, reuse those images across
  K=4 and future runs, retain the images locally to avoid Copilot CLI download
  rate limits, support Docker-based inspection/restart, and provide bounded
  image retention and cleanup. Earlier test runs demonstrated that repeated
  CLI downloads can fail due to package-registry throttling.
- Post-MVP scale work includes resource admission and validated host profiles
  for N=160 and N=256 workloads, including Docker, disk, process, network, and
  Copilot API limits.
- The 64-vCPU host profile uses network-attached storage. Its lower throughput
  and higher latency must be measured explicitly. The MVP should retain
  Wharf's no-derived-agent-image startup path because Pier image builds
  exceeded ten minutes at full load and capped that host near N=80.
- Native Windows, Kubernetes, multi-node orchestration, hosted control-plane
  work, licensing automation, non-Ubuntu distributions, Claude Code, Codex,
  and Java
  remain out of scope.

## Release arc

Arc 1 creates a trustworthy single-task vertical slice. Arc 2 turns that slice
into an operable, recoverable, reportable, packaged v1 system.

### Arc 1: foundation and one-task execution

Sessions 1-10, tags `0.1.0` through `0.10.0`.

The arc is complete when one local DeepSWE task can be loaded, prepared, run
through pinned Copilot CLI, patched, and verified in separate environments with
durable versioned evidence.

### Arc 2: overnight operation and v1 release

Sessions 11-20, tags `0.11.0` through `0.20.0`.

This arc is a qualification checkpoint, not MVP completion. MVP remains
blocked until a real agent/verifier `run`, a real end-to-end `smoke`, and
HydraFusion support are proven on documented Ubuntu host profiles.

### Arc 3: MVP completion and HydraFusion

Sessions 21-24, tags `0.21.0` onward.

This arc wires the existing execution services into the operator CLI, replaces
the local smoke fixture with a real container-backed path, adds the HydraFusion
provider boundary, and reruns the full acceptance matrix before the first
actual MVP/1.0 release.

## Session 1: .NET CLI foundation

Target tag: `0.1.0`
Expected duration: 45-75 minutes. Finishing early is preferred to pulling in
session 2.

### Frame

- Goal: ship a small, production-quality Wharf executable whose command,
  configuration, logging, version, help, completion, and test foundations are
  stable enough for every later session to build on.
- Out of scope: manifest parsing, Docker access, host checks, run-state
  creation, Harbor loading, Copilot execution, and real implementations of
  `check`, `smoke`, `run`, `resume`, `status`, `stop`, or `report`.
- Failure condition: later commands would need to bypass or replace the root
  command, configuration precedence, logger, version source, exit-code
  boundary, or testing/build workflow established here.

### Required research decisions

- Select and record the supported .NET 10 SDK and pin it in `global.json`.
- Confirm a maintained .NET command-line parser and keep it behind a
  Wharf-owned command boundary.
- Use a typed configuration layer with explicit environment lookup so
  precedence remains observable and testable.
- Use Microsoft.Extensions.Logging concepts behind a Wharf-owned logging
  interface. Keep console rendering
  and durable JSON logging as separate sinks.
- Choose the single source version value and its MSBuild override mechanism
  before writing version tests.

### Build

- Create an SDK-style .NET executable and test project and commit the pinned
  SDK configuration.
- Add `src/wharf/Program.cs` as the thin process entry point.
- Keep command construction, configuration, logging, versioning, and exit-code
  behavior in reusable namespaces under `src/wharf`.
- Define the effective version in exactly one easy-to-find source value.
- Support a documented MSBuild version override for release builds.

- Add the v1 command tree:
  `check`, `smoke`, `run`, `resume`, `status`, `stop`, `report`,
  `completion`, and `version`.
- Operational commands may return an explicit not-implemented error in `0.1.0`;
  they must not simulate success.
- Implement `wharf version` and `wharf --version` with semver-only stdout.
- Implement root and command-specific `--help` and `-h`.
- Generate Bash and Zsh completion through `wharf completion`.
- Reserve `-v` for repeatable verbosity and support `--verbose`, `-vv`, and
  `-vvv`.
- Add quiet mode, default tab-delimited console logs, optional JSON console
  logs, and an always-structured JSON logger API for future durable logs.
- Establish secret-aware typed log attributes and a redaction hook. Do not
  claim full secret-safety acceptance until session 8.
- Establish explicit environment-name constants and parsing helpers. Session 1
  proves flag-over-environment-over-default precedence for the global settings
  it owns; manifest precedence arrives in session 2.
- Define stable exit-code constants and one process boundary that maps typed
  errors to exit codes.
- Reject unknown commands and flags.
- Add one documented validation command that formats, analyzes, unit
  tests, and builds a fresh clone.
- Add focused unit and golden tests for help, version source and MSBuild
  override, command registration, unknown input, environment handling, log
  formats, verbosity, quiet mode, redaction hook, and exit-code mapping.
- Update repository memory with the SDK version, project boundaries, validation
  command, and non-obvious CLI decisions.

### Proof

- The repository validation command is green.
- `wharf version` and `wharf --version` print exactly `0.1.0`.
- A test build with the MSBuild version override prints the injected version.
- Root and subcommand help are golden-tested.
- Bash and Zsh completion generation succeeds.
- Default output is tab-delimited; JSON output is one valid object per line.
- `-v`, `-vv`, and `-vvv` reach distinct logger levels.
- Quiet mode suppresses nonessential console output without disabling the
  structured logger.
- Unknown commands and flags return the invalid-input exit code.
- No operational command returns false success.

### Ledger traceability

- Primary: `CON-012`-`CON-034`, `REQ-040`-`REQ-055`, `REQ-105`,
  `REQ-109`-`REQ-111`, `REQ-188`, `REQ-189`, `DEL-001`.
- Partial foundations only: `CON-056`, `CON-057`, `REQ-193`, `AC-013`,
  `AC-014`.
- Deliberately deferred from the same source headings: `CON-035`-`CON-055`,
  `REQ-056`, `REQ-057`, `REQ-104`, `REQ-106`-`REQ-108`,
  `REQ-112`-`REQ-117`.

### Next session starter

Load a versioned run manifest into the typed configuration pipeline and prove
the full flag, environment, manifest, default precedence contract.

## Session 2: reproducible configuration and dry-run

Target tag: `0.2.0`

### Frame

- Goal: load, merge, validate, redact, and print a versioned run
  configuration without creating state or touching Docker.
- Out of scope: task discovery, host validation, scheduling, and execution.
- Failure condition: effective configuration cannot be reproduced exactly or a
  secret can enter its persisted/printed representation.

### Build and proof

- Define the v1 manifest schema and typed defaults.
- Implement flag, environment, manifest, default precedence.
- Implement `K`, `N`, `B`, per-phase timeout, retry, output, model, effort, CLI
  version, mount, artifact, retention, and resource-policy validation.
- Reject unknown keys and unsupported enum values.
- Implement `--dry-run` as validation plus a side-effect-free plan shell.
- Golden-test manifests, effective configuration, precedence, unknown values,
  redaction, and failure-before-output-directory behavior.

### Traceability

- `CON-035`-`CON-057`, `REQ-054`-`REQ-056`, `REQ-191`, `REQ-194`.

### Next session starter

Create the durable event and summary contracts that a validated run will use.

## Session 3: durable state contracts

Target tag: `0.3.0`

### Frame

- Goal: persist versioned append-only events and atomically materialized run
  summaries with immutable identifiers.
- Out of scope: Docker, agents, scheduling policy, and report presentation.
- Failure condition: interruption can corrupt state or replay changes a
  completed logical attempt's identity.

### Build and proof

- Define run, trial, logical-attempt, and infrastructure-retry IDs.
- Define versioned event, status, and effective-configuration envelopes.
- Implement append, replay, atomic summary replacement, and read-only access.
- Make repeated event application idempotent.
- Add corruption, partial-write, concurrent-reader, and schema golden tests.

### Traceability

- `CON-058`-`CON-066`, `DEL-002`, `REQ-005`-`REQ-029`, `REQ-191`.

### Next session starter

Load Harbor-compatible DeepSWE tasks into immutable, checksummed task records.

## Session 4: Harbor task loading and selection

Target tag: `0.4.0`

### Frame

- Goal: discover, validate, checksum, filter, and deterministically order
  Harbor-compatible DeepSWE tasks.
- Out of scope: container creation and agent execution.
- Failure condition: task order or selected task identity changes across
  equivalent runs.

### Build and proof

- Parse the required Harbor task fields and reject unsupported variants.
- Preserve task files and compute source checksums.
- Implement task, language, shard, sampling, include, and exclude selection.
- Derive deterministic seed behavior and K-attempt expansion.
- Add local fixtures and golden ordering tests.

### Traceability

- `REQ-058`-`REQ-074`, `REQ-190`, `AC-004` foundation.

### Next session starter

Implement supported-host and Docker preflight without mutating the host.

## Session 5: check and Docker preflight

Target tag: `0.5.0`

### Frame

- Goal: make `wharf check` accurately classify supported Ubuntu/WSL hosts,
  Docker readiness, capacity, storage, limits, and network prerequisites.
- Out of scope: changing daemon configuration, pulling images, and paid work.
- Failure condition: an unsupported or unsafe host can pass check silently.

### Build and proof

- Detect Ubuntu 24.04 amd64 and WSL2 explicitly.
- Add the documented unsupported-host override with a prominent warning.
- Inspect Docker daemon reachability, storage, networking, file descriptors,
  inotify, CPU, memory, and required capacity.
- Keep all checks read-only and classify failures with stable exit codes.
- Cover probes with fakes plus a supported-host Docker integration test.

### Traceability

- `CON-001`-`CON-011`, `CON-067`-`CON-077`, `REQ-041`, `REQ-116`,
  `REQ-194`, `AC-002` foundation.

### Next session starter

Build a rate-limit-aware image puller with complete structured evidence.

## Session 6: image pulls and 429 control

Target tag: `0.6.0`

### Frame

- Goal: pull required images once with bounded, jittered, decaying 429 retries
  and complete telemetry.
- Out of scope: derived image builds and trial containers.
- Failure condition: a 429 is invisible in durable evidence or a deterministic
  failure is retried as rate limiting.

### Build and proof

- Classify registry failures and implement capped exponential backoff, jitter,
  and pressure decay.
- Coalesce duplicate pulls and reuse local images.
- Record registry, image, attempt, delay, and outcome for every 429.
- Aggregate default console warnings while exposing attempts at `-vv`/`-vvv`.
- Use deterministic clocks and random sources in tests.

### Traceability

- Pull/retry portions of `CON-078`-`CON-099`, `REQ-104`-`REQ-111`,
  `AC-011`-`AC-013`.

### Next session starter

Extend image preparation through the full B-gated derived-build pipeline.

## Session 7: derived image build pipeline

Target tag: `0.7.0`

### Frame

- Goal: prepare reusable agent images behind one complete `B` concurrency gate.
- Out of scope: running Copilot and verifier containers.
- Failure condition: base pulls escape the gate or identical builds run twice.

### Build and proof

- Apply `B` across base-image pull, context preparation, and derived build.
- Build deterministic cache keys from base image, scripts, versions, and
  configuration.
- Coalesce identical in-flight builds and reuse successful images.
- Separate force-build from normal cache behavior.
- Add concurrency, cancellation, deduplication, and failure-cleanup tests.

### Traceability

- Build/gate portions of `CON-078`-`CON-099`, `CON-043`-`CON-046`,
  `AC-009`, `AC-010`.

### Next session starter

Pin, validate, and invoke Copilot CLI without leaking credentials.

## Session 8: Copilot adapter and secret boundary

Target tag: `0.8.0`

### Frame

- Goal: install or select a pinned Copilot CLI, validate model/effort options,
  and construct a secret-safe headless invocation.
- Out of scope: full task execution and patch verification.
- Failure condition: requested and effective versions differ silently or a
  synthetic credential appears in arguments, configuration, or logs.

### Build and proof

- Define the v1 adapter interface without implementing future adapters.
- Pin and record requested/effective Copilot CLI, model, native effort,
  normalized frontier effort, and subagent models.
- Support environment, file, and stdin credential sources.
- Restrict adapter-declared endpoints and redact commands at `-vvv`.
- Add fake-CLI contract tests and synthetic-secret scans.

### Traceability

- `REQ-089`-`REQ-103`, `CON-100`-`CON-115`, `AC-003`, `AC-014`.

### Next session starter

Run Copilot in an isolated agent container and retain its complete trajectory
and patch.

## Session 9: agent execution and patch capture

Target tag: `0.9.0`

### Frame

- Goal: execute one prepared task in an isolated agent environment and retain
  the resulting patch, logs, trajectory, timing, and usage.
- Out of scope: verifier execution and multi-task scheduling.
- Failure condition: the agent can access hidden verifier material or a
  completed patch is not durably retained.

### Build and proof

- Create unprivileged agent containers with explicit mounts and egress.
- Keep hidden tests, verifier credentials, and reference solutions absent.
- Capture stdout, stderr, trajectory, exit state, patch, timing, tokens, steps,
  requests, and available cost data.
- Treat an empty patch as unfinished rather than successful.
- Add a fake-agent Docker integration fixture.

### Traceability

- Agent-side `REQ-058`-`REQ-065`, `REQ-075`-`REQ-088`,
  `REQ-118`-`REQ-128`, `CON-100`-`CON-115`, `AC-005` foundation.

### Next session starter

Apply the retained patch to a pristine verifier and complete the single-task
vertical slice.

## Session 10: pristine verification vertical slice

Target tag: `0.10.0`

### Frame

- Goal: verify one retained agent patch in a separate pristine environment and
  persist a trustworthy result.
- Out of scope: concurrent datasets, resume, scoring aggregates, and polished
  reports.
- Failure condition: verification reuses the agent workspace or exposes
  verifier-only assets to the agent.

### Build and proof

- Create a pristine verifier environment from the task source.
- Apply the retained patch and run configured verifier commands and timeouts.
- Isolate verifier mounts, credentials, network, and logs.
- Persist raw verifier evidence plus a versioned trial result.
- Add an end-to-end fake-agent fixture proving hidden-test separation.

### Traceability

- Verifier-side `REQ-058`-`REQ-065`, `REQ-118`-`REQ-128`,
  `CON-100`-`CON-115`, `AC-004`, `AC-005`.

### Arc 1 checkpoint

- Run the mid-arc reflection after session 5 and an arc checkpoint now.
- Demonstrate a retained patch and verifier result from one DeepSWE-compatible
  task.
- Reconcile all parking-lot items before starting scheduler work.

### Next session starter

Expand the proven one-task path into a deterministic work-conserving scheduler.

## Session 11: deterministic dataset scheduler

Target tag: `0.11.0`

### Frame

- Goal: schedule the selected task-attempt graph deterministically with an `N`
  trial limit and observable lifecycle states.
- Out of scope: retry policy and restart recovery.
- Failure condition: scheduling order changes results or completed work is not
  banked immediately.

### Build and proof

- Implement the deterministic pending queue and work-conserving admission.
- Enforce `N` without weakening `B`.
- Persist lifecycle transitions and bank successful logical attempts.
- Expose pending, preparing, starting, agent, verifier, completed, errored, and
  cancelled counts.
- Add deterministic scheduler and cancellation tests.

### Traceability

- `REQ-066`-`REQ-088`, `REQ-104`-`REQ-108`, `GOAL-007`.

### Next session starter

Add phase-specific resources, timeouts, and infrastructure retry semantics.

## Session 12: resource limits, timeouts, and retries

Target tag: `0.12.0`

### Frame

- Goal: enforce phase-specific resource and timeout policies while separating
  infrastructure retries from logical attempts.
- Out of scope: process restart and operator stop.
- Failure condition: an infrastructure retry changes K or double-counts a
  logical result.

### Build and proof

- Add build, agent, and verifier resource gates.
- Implement timeout multipliers and explicit overrides.
- Classify retryable and excluded failures with bounded retries.
- Persist infrastructure-retry IDs and evidence separately.
- Test saturation, fairness, cancellation, timeout, and retry accounting.

### Traceability

- `CON-038`-`CON-049`, `CON-058`-`CON-066`,
  `REQ-075`-`REQ-088`, `AC-007` foundation.

### Next session starter

Recover an interrupted run from retained events without repeating completed
work.

## Session 13: interruption recovery and resume

Target tag: `0.13.0`

### Frame

- Goal: make `wharf resume` reconstruct state and continue only unfinished or
  retryable work.
- Out of scope: operator stop cleanup and reports.
- Failure condition: completed attempts rerun, retry counts change, or partial
  state is mistaken for success.

### Build and proof

- Reconcile events, summaries, artifacts, and live Docker resources.
- Recover interruptions during pull, build, setup, agent, verifier, and result
  persistence.
- Preserve deterministic task order and immutable IDs.
- Inject interruption at every required phase in integration tests.

### Traceability

- `CON-058`-`CON-066`, `REQ-044`, `REQ-075`-`REQ-088`,
  `REQ-192`, `AC-007`.

### Next session starter

Make active runs observable and safely stoppable without deleting evidence.

## Session 14: status, stop, and notifications

Target tag: `0.14.0`

### Frame

- Goal: provide non-mutating status, safe stop, retained evidence, and durable
  completion/blocked notifications.
- Out of scope: score computation and final report styling.
- Failure condition: status mutates state or stop removes any retained file.

### Build and proof

- Implement concurrent read-only `status` with lifecycle counts, occupied
  concurrency, retries, and ETA.
- Implement graceful stop followed by targeted removal of only ephemeral
  Docker resources.
- Always write durable completion and blocked-run status.
- Add the optional webhook behind an explicit configuration setting.
- Test stop during each execution phase and assert byte-for-byte file
  retention.

### Traceability

- `REQ-045`, `REQ-046`, `REQ-057`, `REQ-104`-`REQ-117`,
  `AC-006`, `AC-008`.

### Next session starter

Audit retained evidence and compute PASS@1 and Enterprise Score from fixtures.

## Session 15: audit and scoring

Target tag: `0.15.0`

### Frame

- Goal: derive trustworthy trial/task/run metrics from retained evidence with
  explicit completeness.
- Out of scope: Markdown presentation and cross-run comparison.
- Failure condition: missing measurements become zero or PASS without explicit
  incompleteness.

### Build and proof

- Audit expected versus observed tasks and attempts.
- Compute PASS@1 and the specified task-equal Enterprise Score.
- Compute test coverage, regressions, cost, tokens, requests, steps, agent and
  trial time, and coverage completeness.
- Distinguish missing, invalid, partial, and zero values.
- Match existing Enterprise Score and cost fixtures.

### Traceability

- `REQ-118`-`REQ-152`, `AC-015`, `AC-016`.

### Next session starter

Render deterministic static and machine reports from audited retained state.

## Session 16: deterministic reporting

Target tag: `0.16.0`

### Frame

- Goal: regenerate identical Markdown and machine-readable reports from the
  same retained job.
- Out of scope: hosted dashboards and post-v1 repair comparisons.
- Failure condition: a report depends on transient process state or changes
  when rerun over identical evidence.

### Build and proof

- Implement `wharf report` over retained artifacts only.
- Emit static Markdown plus versioned JSON/JSONL.
- Include configuration, provenance, score, cost, usage, timing, coverage,
  retry, and 429 completeness.
- Support the portable data-only manifest and existing Pier 0.3.1 migration
  input.
- Golden-test deterministic regeneration.

### Traceability

- `REQ-129`-`REQ-152`, `REQ-181`-`REQ-187`, `DEL-003`,
  `AC-016`, `AC-017`.

### Next session starter

Turn the full path into an operator-facing smoke command and shared Ubuntu
bootstrap.

## Session 17: smoke and shared bootstrap

Target tag: `0.17.0`

### Frame

- Goal: provision a supported Ubuntu host and run a complete smoke task through
  check, agent, verifier, artifacts, and report.
- Out of scope: release packaging signatures and full DeepSWE scale.
- Failure condition: cloud, on-premises, and WSL bootstrap paths configure
  different products.

### Build and proof

- Create one idempotent Ubuntu bootstrap with thin cloud-init and WSL wrappers.
- Apply and validate required Docker daemon, network, kernel, storage,
  descriptor, and inotify settings with explicit sudo.
- Implement `wharf smoke` using a deterministic local fixture.
- Prove Ubuntu 24.04 and WSL2 paths or document an explicit environment-backed
  qualification gate when WSL hardware is unavailable in-session.

### Traceability

- `CON-001`-`CON-011`, `CON-067`-`CON-077`,
  `CON-116`-`CON-118`, `REQ-030`-`REQ-039`, `REQ-042`,
  `AC-001`, `AC-002`.

### Next session starter

Package the proven binary and bootstrap as an auditable signed Ubuntu release.

## Session 18: package, supply chain, and upgrade

Target tag: `0.18.0`

### Frame

- Goal: produce an installable `.deb` with checksums, SBOM, signature flow,
  upgrade, and rollback.
- Out of scope: hosted update services and licensing.
- Failure condition: installed version metadata differs from the tagged
  executable or rollback cannot restore the prior release.

### Build and proof

- Build release binaries with the standard linker version override.
- Package the binary, completions, bootstrap integration, and documentation.
- Generate checksums and an SBOM; document and test signing verification.
- Test clean install, supported upgrade, and rollback on Ubuntu 24.04.
- Re-run synthetic-secret and least-privilege checks on package artifacts.

### Traceability

- `CON-015`-`CON-020`, `CON-100`-`CON-124`, `AC-001`, `AC-014`.

### Next session starter

Measure and publish conservative host profiles under the overnight workload.

## Session 19: overnight scale qualification

Target tag: `0.19.0`

### Frame

- Goal: qualify and document the 32-concurrent and 88-or-greater host profiles
  with no completed work lost.
- Out of scope: changing product semantics to chase benchmark throughput.
- Failure condition: results lack exact hardware, deadline, configuration,
  cost, or retained evidence.

### Build and proof

- Resolve `OQ-003` with exact hardware and overnight deadlines.
- Implement conservative recommendation output that never raises operator
  limits silently.
- Run DeepSWE K=4 in named `tmux` sessions on both documented profiles.
- Verify throughput, resource pressure, retries, recovery, score/cost
  completeness, and no completed-trial loss.
- Publish the measured profiles and qualification evidence.

### Traceability

- `GOAL-002`, `GOAL-003`, `GOAL-007`-`GOAL-009`,
  `CON-119`-`CON-123`, `DEL-004`, `AC-020`.

### Next session starter

Run the complete acceptance matrix, close release ownership, and cut v1.

## Session 20: v1 release qualification

Target tag: `0.20.0`

### Frame

- Goal: preserve a qualified release checkpoint while explicitly deferring MVP
  until real `smoke`, `run`, and HydraFusion support are complete.
- Out of scope: post-v1 repair/replay, Claude Code, Codex, Java, and .NET.
- Failure condition: any acceptance criterion is waived without a written,
  source-approved scope change.

### Build and proof

- Resolve `OQ-001` by naming the release owner.
- Run the full format, lint, unit, integration, smoke, package, security,
  upgrade, rollback, report-reproduction, and host-profile matrix.
- Validate the requirements ledger and produce a requirement-to-test coverage
  report.
- Re-run secret scanning over arguments, configuration, logs, reports,
  exports, packages, and fixtures.
- Confirm every v1 non-goal remains absent.
- Publish release notes, operator documentation, checksums, SBOM, and signed
  package.
- Fast-forward merge, tag and push `0.20.0`, update repo memory and session log,
  and record the deferred MVP gates.

### Traceability

- All v1 `GOAL`, `NG`, `CON`, `DEC`, `REQ-001`-`REQ-152`,
  `REQ-181`-`REQ-194`, `DEL-001`-`DEL-004`, `AC-001`-`AC-018`,
  `AC-020`, `OQ-001`.

## Coverage summary

- Session 1 establishes the cross-cutting CLI and engineering contract.
- Sessions 2-4 establish reproducible input and durable state.
- Sessions 5-10 establish the supported one-task execution path.
- Sessions 11-14 establish unattended scheduling, recovery, and operations.
- Sessions 15-16 establish trustworthy scoring and reporting.
- Sessions 17-18 establish supported provisioning and distribution.
- Sessions 19-20 establish scale evidence and release qualification.
- Post-v1 requirements remain visible but are intentionally not scheduled in
  this MVP plan.

## Session 21: real run wiring

Target tag: `0.21.0`

- Wire `wharf run` through task loading, image preparation, agent, verifier,
  scheduler, recovery, audit, and reporting boundaries.
- Fail if a non-dry-run command remains a placeholder or reports success
  without retained evidence.

## Session 22: HydraFusion provider

Target tag: `0.22.0`

- Add a HydraFusion adapter with version pinning, effort mapping, endpoint
  allowlisting, credential redaction, usage/cost evidence, and provider-neutral
  agent contracts.
- Keep HydraFusion secrets and provider-specific behavior behind the adapter.

## Session 23: real smoke path

Target tag: `0.23.0`

- Replace the local fixture-only smoke path with a real deterministic
  container-backed task that invokes the same path as `run`.
- Retain patch, verifier, report, and host evidence.

## Session 24: MVP qualification

Target tag: `0.24.0`

- Rerun the complete acceptance matrix and cut the first honest MVP/1.0
  release only after real `smoke`, `run`, and HydraFusion evidence passes.
