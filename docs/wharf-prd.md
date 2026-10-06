# Wharf Product Requirements

Status: Draft v0.1
Date: 2026-10-06
Owner: Not specified
Sources: `docs/wharf.md` SHA-256 `3e46bc8a12da5b7c74142f044777e734ff77157ae3e784d3cd18269312271ae7`
Machine companion: `docs/wharf.jsonl`

## 1. Overview

Wharf is a simple, scalable, enterprise-focused CLI that replaces Pier while
remaining compatible with Harbor task definitions. It is built for enterprise
evaluation engineers who run coding-agent benchmarks on customer-owned Ubuntu
infrastructure and for engineering leaders who consume the resulting model,
cost, and quality comparisons. V1 establishes reliable local execution,
durable evidence, recovery, and reporting with DeepSWE and Copilot CLI before
Wharf expands into repair/replay and additional agents. [Source: `docs/wharf.md` >
Users, Product goals, Release scope and phases]

### 1.1 Deliberately Unspecified

These choices are intentionally left to implementation as long as the fixed
contracts and acceptance criteria are met.

| Fixed here | Left to the implementer |
|------------|-------------------------|
| Stable CLI behavior and commands | A maintained .NET command-line parser behind a Wharf-owned boundary |
| .NET SDK-style executable structure | Responsibility-based namespaces under `src/wharf` |
| Versioned manifest, event, result, summary, and log contracts | Internal event-store implementation |
| Host checks and operator safety limits | Exact concurrency recommendation algorithm |
| Required report content and machine schemas | Human report styling |
| Build-pipeline gate and 429 behavior | Internal scheduling data structures |

Wharf must use normal .NET project organization, keep `Program.cs` thin, and
satisfy the observable contracts regardless of these internal
choices. [Source: `docs/wharf.md` > Language, State model, Reporting and comparison,
Delivery and support]

## 2. Goals

- [must] Provide a simple provision, check, smoke, run, resume, status, stop,
  and report workflow. [Source: Product goals, CLI contract]
- [must] Complete one trustworthy DeepSWE K=4 evaluation overnight without
  operator attention on each documented host profile. [Source: Product goals]
- [must] Scale from a validated 32-concurrent on-premises profile to a
  validated 88-concurrent or larger cloud profile without losing completed
  trials. [Source: Product goals]
- [must] Keep source code, hidden tests, patches, credentials, run state, and
  results under customer control. [Source: Users, Product goals]
- [must] Preserve Harbor task compatibility and proven Pier behavior without
  runtime monkey patches. [Source: Product goals, Task compatibility]
- [must] Produce trustworthy PASS@1, Enterprise Score, cost, usage, runtime,
  and coverage reports from retained evidence. [Source: Release scope and
  phases, Auditing and trustworthy scoring]
- [must] Run on supported Ubuntu infrastructure across public cloud, private
  cloud, on-premises, and WSL2. [Source: Supported platform]
- [should] Make later repair/replay and additional task providers possible
  without implementing distributed execution in v1. [Source: Initial scope,
  Release scope and phases]

## 3. Non-Goals

V1 does not include:

- Native Windows workers or native PowerShell execution.
- Kubernetes deployment.
- Multi-node orchestration.
- A hosted control plane or dashboard.
- Licensing or task-redistribution automation.
- Linux distributions other than Ubuntu 24.04 LTS.
- Byte-for-byte reproduction of Pier internals.
- Claude Code or Codex adapters; these arrive in v1.1.
- Automated repair and replay; these follow stable v1 execution and reporting.
- A second implementation language; .NET is the canonical Wharf implementation.

[Source: `docs/wharf.md` > Non-goals for v1, Release scope and phases]

## 4. Architecture

### 4.1 Components

| Component | Purpose | Notes |
|-----------|---------|-------|
| `wharf` CLI | Public command surface and orchestration entry point | .NET executable under `src/wharf`; stable exit codes |
| Bootstrap and package | Provision a supported Ubuntu node | Signed `.deb`; shared installer used by cloud-init, on-premises, and WSL |
| Check and smoke | Validate the host and full execution path | Must fail before paid work on invalid hosts or configuration |
| Dataset/task loader | Read Harbor task and dataset definitions | DeepSWE is the initial provider |
| Scheduler | Order, admit, retry, resume, and drain logical attempts | Work-conserving; deterministic order; no distributed coordination in v1 |
| Docker worker | Prepare images and run isolated agent and verifier environments | `B` gates the complete image-preparation/build pipeline |
| Copilot adapter | Install and run a pinned Copilot CLI headlessly | Records requested and effective CLI, model, effort, and subagent models |
| State store | Persist append-only events and materialized summaries | Atomic writes; idempotent recovery; versioned schemas |
| Artifact store | Retain patches, logs, trajectories, verifier evidence, and cost | Retain everything in v1 |
| Reporter | Recompute static and machine-readable output | PASS@1, Enterprise Score, cost, usage, runtime, coverage |

Component names are derived labels for boundaries directly required by
`docs/wharf.md`; internal package names remain deliberate implementation choices.

### 4.2 Shape

The CLI loads and validates a versioned run manifest, task dataset, adapter,
and host configuration before consuming model quota. The scheduler creates
logical attempts and admits them through the configured build, agent, and
verifier limits. The Docker worker prepares a reusable agent image, runs the
agent with only adapter-allowed network access, saves the patch, and applies it
in a pristine verifier environment that contains the hidden tests. Every state
transition and artifact is retained. Status reads materialized summaries
without mutating the run; reporting can be rerun from retained artifacts after
execution completes. [Source: Run configuration, Scheduling and lifecycle,
State model, Task compatibility, Results and artifacts]

## 5. Data

### 5.1 Source and format

- Harbor task directories provide task metadata, instructions, environment
  definitions, artifacts, tests, verifier settings, and checksums.
- DeepSWE is the initial dataset.
- Existing Pier 0.3.1 job directories are accepted as migration input.
- Wharf stores versioned configuration, append-only events, job and trial
  summaries, artifacts, and portable analytics records.
- `docs/wharf.jsonl` is the canonical LLM requirements handoff; it is not runtime
  trial data. [Source: Task compatibility, Features, State model, Requirements
  artifacts]

### 5.2 Packaging

- Runtime outputs remain in customer-owned storage.
- Wharf emits a documented Pier-compatible trial artifact subset plus additive
  Wharf metadata.
- Reports are static Markdown and machine-readable JSON or JSONL.
- The data-only reporting manifest does not require raw jobs. [Source: Features,
  Auditing and trustworthy scoring]

### 5.3 Loading

- Task and dataset definitions are discovered from the selected local path.
- Run configuration is merged in this precedence order: command flag,
  environment variable, manifest, built-in default.
- Existing completed logical attempts are detected and banked on resume.
- Empty patches, incomplete attempts, and quota failures remain unfinished.
  [Source: CLI contract, Run configuration, Task ordering and selection]

### 5.4 Seed data

- DeepSWE tasks are the primary development and acceptance dataset.
- Smoke tasks validate the host, agent, verifier, artifact, and report path.
- Local golden fixtures cover Harbor parsing, state recovery, artifacts,
  versioned schemas, scoring, and reports. [Source: Initial scope, Development
  process]

### 5.5 Schema

Wharf must version its run manifest, effective configuration, events, job and
trial results, materialized summaries, structured logs, reports, and exports.
Run, trial, logical-attempt, and infrastructure-retry identifiers are immutable.
Events are append-only. Mutable summaries use atomic file replacement. Internal
storage structures are deliberately unspecified. [Source: Run configuration,
State model, Progress and operations]

## 6. Interface Surface

| Command | Availability | Contract |
|---------|--------------|----------|
| `wharf check` | v1 | Validate supported host, Docker, limits, network capacity, storage, and required images |
| `wharf smoke` | v1 | Exercise the complete supported execution path |
| `wharf run` | v1 | Start a configured task or dataset run |
| `wharf resume` | v1 | Resume missing or retryable work without duplicating completed attempts |
| `wharf status` | v1 | Read current lifecycle counts and ETA without mutating the run |
| `wharf stop` | v1 | Stop execution and remove only ephemeral Docker resources |
| `wharf report` | v1 | Generate static and machine-readable reports from retained evidence |
| `wharf completion` | v1 | Generate Bash and Zsh completion |
| `wharf version` | v1 | Print only the effective semantic version |
| `wharf repair` | post-v1 | Run repair/replay from a retained patch and sanitized failure summary |
| `wharf compare` | post-v1 | Compare fresh, repair, model, or CLI-version runs |

Global CLI behavior:

- `--help` and `-h` display command-specific help.
- `--version` and `wharf version` print the semantic version with no additional
  text or punctuation.
- `--dry-run` and `-d` validate and show the plan without creating state,
  modifying Docker, or consuming quota.
- `--verbose` and `-v` add lifecycle and retry detail; `-vv` logs every 429
  retry and selected delay; `-vvv` adds redacted command and subprocess detail.
- `-v` is reserved for verbosity.
- Unknown commands, flags, configuration keys, models, effort values, and
  adapter options fail explicitly.
- Stable exit codes distinguish success, invalid input, unsupported host,
  blocked run, partial run, and internal failure.

[Source: `docs/wharf.md` > Language, CLI contract]

### 6.1 Validation rules

- `K` is the number of attempts per task; full DeepSWE defaults to `K=4`.
- `N` limits concurrent trials.
- `B` limits the complete image-preparation/build pipeline, including required
  base-image pulls.
- V1 exposes no separate pull semaphore or pull-concurrency flag.
- CLI versions, models, effort values, and adapter options must be recognized.
- Unsupported hosts fail check unless the explicit unsupported-host override
  is present.
- Configuration and host validation complete before output directories or paid
  model work begin.
- Secret values are excluded from persisted effective configuration.
- Per-phase timeout multipliers and explicit overrides must be valid for their
  phases.

[Source: Run configuration, Supported platform, Image pulls and builds]

## 7. Observability

### 7.1 Metrics

Wharf reports:

- Pending, pulling image, building image, starting environment, running agent,
  verifying, completed, errored, cancelled, and retry counts.
- Occupied concurrency and estimated completion time.
- PASS@1 and Enterprise Score.
- Cost per trial, task, and passing trial.
- Agent-only and total trial wall time.
- Input, output, and cache tokens; steps; provider and premium requests.
- Score and cost coverage completeness.
- Total 429 responses, affected images, exhausted retries, retry counts, and
  cumulative retry delay.

[Source: Features, Progress and operations, Auditing and trustworthy scoring,
Image pulls and builds]

### 7.2 Logging

- Durable structured logs are independent of console rendering and use a
  versioned JSON schema.
- Human output is tab-delimited by default and is not a machine API.
- Default output is concise and emits one aggregate warning for each image pull
  that encountered 429 responses.
- Every 429 is always recorded durably with registry, image, attempt, selected
  delay, and eventual outcome.
- `-vv` and `-vvv` display each retry as it happens.
- Secrets are redacted at every verbosity level.
- Quiet mode suppresses nonessential console output without suppressing
  durable evidence. [Source: Language, Image pulls and builds, Progress and
  operations]

### 7.3 Dashboards

Not applicable to v1. Wharf writes static Markdown and machine-readable exports.
A hosted dashboard and control plane are explicit non-goals. An optional webhook
may report completion or a blocked run; durable status files are mandatory.
[Source: Non-goals for v1, Progress and operations, Reporting and comparison]

## 8. Deployment

- Supported hosts are Ubuntu 24.04 LTS amd64 and Ubuntu 24.04 LTS on WSL2.
- The supported unit is a host provisioned by the official idempotent bootstrap.
- Wharf is delivered as a versioned, signed `.deb`.
- Cloud-init, on-premises, and WSL entry points invoke the same installer.
- Bootstrap applies and validates required Docker daemon, network, kernel,
  storage, file-descriptor, and inotify configuration.
- Host or Docker modifications use explicit sudo.
- Wharf runs with least privilege and trial containers are unprivileged by
  default.
- Dedicated hosts are recommended when Docker daemon changes could affect
  shared workloads.
- Public cloud is an acceleration option, not a requirement.

[Source: Supported platform, Docker and host behavior, Secrets and enterprise
controls, Delivery and support]

## 9. Build and Packaging

- Implement Wharf in idiomatic .NET with a thin `Program.cs` and reusable
  responsibility-based namespaces under `src/wharf`.
- Define the version in exactly one easy-to-find source value.
- Commands, reports, artifacts, and package metadata read that variable.
- Local builds use its source default.
- Release builds override the same value through a documented MSBuild property
  or generated assembly metadata value.

- `wharf version` and `wharf --version` return the effective value.
- Document the supported .NET SDK and pin it with `global.json`.
- Produce a signed `.deb`, release checksums, and a software bill of materials.
- Support signed upgrade and rollback between Wharf releases.

[Source: Language, Supported platform, Secrets and enterprise controls,
Delivery and support]

## 10. Testing and Benchmarks

### 10.1 Unit tests

Unit tests cover configuration merging and validation, Harbor parsing,
deterministic ordering, state transitions, atomic summary generation, retry
classification, scoring, secret redaction, and schema validation. Golden
fixtures cover configuration, events, results, reports, Enterprise Score, and
JSONL. [Source: Development process, Acceptance criteria]

### 10.2 Integration tests

Integration tests cover Docker host preflight, image deduplication, derived
image reuse, the `B` build-pipeline gate, 429 backoff and telemetry, Copilot
version pinning, patch capture, separate verification, cleanup, interruption,
resume, and report regeneration. Interruptions are injected during pull, build,
agent setup, agent execution, verification, and reporting. [Source: Image pulls
and builds, Agent support, Development process]

### 10.3 End-to-end tests

- Ubuntu and WSL bootstrap must reach passing check and smoke results.
- A DeepSWE task must run through the Copilot adapter, retain its patch, apply
  it to a pristine verifier, and hide verifier tests from the agent.
- Stopping removes only ephemeral Docker resources and retains every file.
- Resume does not rerun or double-count completed logical attempts.
- Reports regenerated from the same retained job are identical.
- Unsupported hosts and invalid configuration fail before quota consumption.
- Synthetic secrets never appear in arguments, configuration, logs, reports,
  or exports.

[Source: Acceptance criteria]

### 10.4 Performance targets

- DeepSWE K=4 is the reference workload.
- Wharf publishes measured 32-concurrent and 88-concurrent-or-greater host
  profiles with hardware specifications and overnight deadlines.
- Each qualified profile completes before its published deadline with no
  completed trials lost.
- A lower-concurrency on-premises or WSL host may take longer without changing
  result semantics.
- Build, agent, and verifier limits protect CPU, disk, memory, and provider
  capacity.

[Source: Product goals, Scheduling and lifecycle, Delivery and support,
Acceptance criteria]

## 11. Configuration

Configuration precedence is command flag, environment variable, manifest, then
built-in default.

| Setting | Flag or variable | Default | Purpose |
|---------|------------------|---------|---------|
| Attempts | `K` / manifest equivalent | `4` for full DeepSWE | Attempts per task |
| Trial concurrency | `N` / manifest equivalent | Host/run profile | Concurrent logical trials |
| Build pipeline concurrency | `B` / manifest equivalent | Available CPU, operator-overridable | Image preparation and builds |
| CLI version | Adapter version setting | Explicit run value | Pin Copilot CLI |
| Model | Adapter model setting | Adapter-defined | Select main model |
| Effort | Adapter effort setting | Model/adapter-defined | Preserve native value and normalized class |
| Timeouts | Phase settings | Task values | Override build, setup, agent, and verifier timeouts |
| Verbosity | `--verbose`, `-v`, `-vv`, `-vvv` | Concise | Console detail |
| Output | Manifest/flag | Run directory | Customer-owned retained state |

Wharf records the effective merged configuration without secret values.
Environment names and manifest keys are versioned contracts. Unknown values
fail explicitly. V1 has no pull-concurrency setting. [Source: Run configuration,
CLI contract]

## 12. Inner-Loop Dev Process

1. Install the documented .NET SDK and Docker prerequisites.
2. Run the repository's single format, lint, unit-test, and integration-test
   command.
3. Run local Harbor parsing, state recovery, artifact, and report fixtures.
4. Run check and smoke on a supported Ubuntu environment.
5. Exercise interruption and recovery fixtures before changing scheduling or
   persistence.
6. Validate golden schemas and reports before changing public data.
7. Build a local development version from the single source version property;
   use the documented MSBuild property only for release-version overrides.

The README must document these steps and the exact validation command.
[Source: Development process, Language]

## 13. Security

- Customer source, tests, patches, credentials, state, and results remain in
  customer-controlled infrastructure and storage.
- Secrets never appear in process arguments, persisted configuration, normal
  logs, reports, or exported artifacts.
- Credentials may come from environment, file, stdin, and future secret-manager
  sources.
- Agent egress is restricted to adapter-declared endpoints.
- The verifier receives no model credentials or agent-only network access.
- Agent and verifier credentials, networks, mounts, and files are isolated.
- Hidden tests and reference solutions never enter agent or repair workspaces.
- Trial containers are unprivileged by default.
- Host changes require explicit sudo.
- Releases include checksums and an SBOM.
- All source snapshots, patches, trajectories, logs, test output, cost data,
  and run state are retained in v1; there is no automatic expiry.
- Deletion requires an explicit prune operation.

[Source: Secrets and enterprise controls, Features]

## 14. Acceptance Criteria

- [ ] The official bootstrap installs v1 on Ubuntu 24.04 LTS amd64.
- [ ] Check and smoke pass on Ubuntu 24.04 LTS and Ubuntu 24.04 LTS on WSL2.
- [ ] The Copilot adapter pins its CLI version and records requested and
  effective versions.
- [ ] A DeepSWE task runs in separate agent and verifier environments without
  exposing hidden tests to the agent.
- [ ] A completed patch is retained and applied to a pristine verifier.
- [ ] Stop removes only Docker processes and ephemeral resources and retains
  every file and directory.
- [ ] Resume does not rerun completed logical attempts or double-count
  infrastructure retries.
- [ ] Status reports every lifecycle state without attaching to tmux or
  mutating the run.
- [ ] The `B` gate limits the complete image-preparation/build pipeline.
- [ ] Duplicate pulls and identical derived-agent builds are coalesced.
- [ ] Registry rate limits use bounded backoff with decay and jitter;
  deterministic failures are not retried as rate limits.
- [ ] Every 429 is present in durable structured logs; affected-image summaries
  include retry count and cumulative delay.
- [ ] Default output aggregates 429 retries per image; `-vv` and `-vvv` show
  every retry.
- [ ] Synthetic secrets are absent from arguments, configuration, logs,
  reports, and exports.
- [ ] PASS@1 and Enterprise Score match golden fixtures.
- [ ] Provider-supplied cost, tokens, steps, agent time, trial time, and coverage
  completeness are reported.
- [ ] The same retained job regenerates the same static and machine-readable
  reports.
- [ ] `docs/wharf.jsonl` parses one object per line, uses unique IDs, resolves every
  reference, and records source provenance.
- [ ] Each documented host profile completes DeepSWE K=4 within its published
  overnight window with no completed trials lost.
- [ ] In the post-v1 repair phase, deterministic summaries pass leakage
  sanitizer tests.

## 15. Open Questions

No blocking questions remain.

| Question | Blocking | Owner | Source gap |
|----------|----------|-------|------------|
| Who is the named PRD and release owner? | No | Not specified | `docs/wharf.md` defines users but not ownership |
| Which exact .NET SDK version is supported? | No | Wharf maintainers | The source requires documentation but does not select a version |
| What hardware and deadlines define the 32- and 88-concurrent profiles? | No | Wharf maintainers | The source requires measured published profiles |

## 16. Provenance

| Source note | Date | Contributed |
|-------------|------|-------------|
| `docs/wharf.md` | 2026-10-06 | All product, scope, platform, interface, execution, persistence, security, reporting, repair, delivery, development, and acceptance requirements |

The machine companion `docs/wharf.jsonl` contains one source-backed record per bullet,
including source heading and line, source hierarchy, phase, priority,
relationships, and acceptance links. The source hash recorded in both artifacts
is `3e46bc8a12da5b7c74142f044777e734ff77157ae3e784d3cd18269312271ae7`.
