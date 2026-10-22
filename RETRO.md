# Wharf development and testing summary through 1.0.1

Wharf was developed as an AI-native, evidence-first benchmark execution system
for running DeepSWE tasks with Copilot CLI on customer-controlled Ubuntu
infrastructure. Its design emphasizes deterministic execution, isolation,
durable evidence, resumability, trustworthy scoring, and operational safety.
The product requirements are captured in `docs/wharf-prd.md`, with the
machine-readable requirements handoff in `docs/wharf.jsonl`.

## Executive summary

Wharf 1.0.0 is the first MVP release of a production-oriented benchmark
execution platform. Across 25 versioned development increments, the project
moved from a CLI foundation to a deterministic, recoverable,
evidence-preserving DeepSWE and Copilot execution platform. The subsequent
`1.0.1` branch work corrected CLI version resolution, made provider-default
effort the default, and extended the retained benchmark comparisons.

* The system now has explicit boundaries for configuration, Harbor task
  loading, host checks, image preparation, Copilot execution, agent isolation,
  verifier isolation, scheduling, retries, recovery, operations, analytics,
  and reporting.
* Agent and verifier environments are isolated. Release 0.24.0 removed host
  bind mounts from both paths and replaced them with explicit container
  lifecycles and evidence transfer, reducing workspace leakage and cleanup
  ambiguity.
* Failures are diagnosable and recoverable. Wharf retains logs, trajectories,
  patches, verifier evidence, timing, usage, cost, retry history, and
  progress events. Resume can continue unfinished attempts without rerunning
  completed work.
* Qualification evidence is measurable. The controlled z01 K=2 benchmark
  passed 156/226 attempts, or 69.03%, and passed at least once on 95/113
  tasks. Known cost was $78.2231 with 97.35% cost-evidence coverage and
  $0.3556 per costed task attempt.
* Validation is layered rather than simulated. The repository contains unit,
  boundary, Docker-backed, smoke, packaging, analytics, and controlled
  benchmark checks. The final release-candidate suite passed 226/226 tests,
  a Release build, formatting verification, and a Docker-backed smoke run
  using Copilot CLI 1.0.96. The post-1.0 CLI version fix subsequently recorded
  229/229 Release tests, formatting verification, and Docker-backed smoke
  using CLI 1.0.98.
* Reporting is designed for auditability. PASS@1 uses the planned trial
  denominator, task-level accuracy is reported separately, infrastructure
  retries do not become extra trials, missing costs remain missing, and
  manifests are validated before scores are reported.
* Sessions and RPI reduced delivery risk. Sessions kept releases small and
  shippable; Research, Plan, Implement, Review artifacts prevented invented
  interfaces, made scope changes visible, and tied implementation claims to
  test evidence.

The 1.0.0 acceptance matrix is closed. Container-backed smoke validation and
unattended K=4 benchmark runs are retained in the repository, with some runs
spanning an overnight window. Through the `1.0.1` branch checkpoint, the
enterprise dataset contains 22 arms and 1232 logical trials with complete
score, cost, and duration coverage. The `1.0.0` tag points to `1ab71c4`; the
`1.0.1` branch checkpoint is `5cc3dac`, with no `1.0.1` tag present at this
review. Post-v1 repair/replay, hosted operations, additional agent products,
and larger scale profiles remain outside the delivered scope.

## Development approach

The project used two nested disciplines:

* Sessions were the outer delivery loop: one focused, release-sized block of
  work, generally 60 to 120 minutes, ending in a tagged release or an
  explicitly documented hold.
* RPI, Research, Plan, Implement, Review, was the inner engineering loop used
  for each substantial change.

Each session defined a goal, explicit out-of-scope items, and a measurable
failure condition. It then produced durable research, plan, changes, and
review artifacts under `.github/tracking/`. A fit check occurred before
implementation so scope could be cut before work accumulated. The close
ritual required tests, a tag, updated repository memory, a session log entry,
and a retrospective note.

This structure is documented in `docs/wharf-sessions.md`, `docs/session-log.md`,
`.github/skills/sessions/SKILL.md`, and `.github/skills/rpi/SKILL.md`.

## Release progression

### Foundation and trustworthy single-task execution: 0.1.0 to 0.10.0

The first arc established the core execution boundaries and a complete
one-task vertical slice.

* `0.1.0` established the canonical .NET CLI foundation, including commands,
  configuration, logging, versioning, help, completion, exit codes, and the
  validation workflow. An earlier Go implementation was withdrawn before the
  supported release arc continued.
* `0.2.0` added strict versioned manifests, explicit
  flag/environment/manifest/default precedence, deterministic dry-run
  planning, and secret redaction.
* `0.3.0` added filesystem-backed durable state with append-only events,
  atomic summaries, idempotent event IDs, contiguous sequence numbers, and
  explicit malformed-record failures.
* `0.4.0` added Harbor task loading, deterministic checksums, filtering,
  sharding, seeded sampling, and K-attempt expansion.
* `0.5.0` added read-only host and Docker preflight, separating platform,
  daemon, and capacity failures.
* `0.6.0` added image preparation with local-store inspection, duplicate
  coalescing, narrowly scoped registry-rate-limit retries, jittered backoff,
  and durable pull evidence.
* `0.7.0` added derived-image coordination with deterministic cache keys,
  shared build gating, successful-result reuse, cancellation-safe waiters,
  and retryable failed builds.
* `0.8.0` added the Copilot adapter boundary with exact CLI version
  validation, effort normalization, HTTPS endpoint allowlisting, and
  credential-source metadata without credential-value propagation.
* `0.9.0` added isolated agent execution with distinct mounts, hidden-verifier
  protection, explicit networking and UID, complete evidence capture, and
  `unfinished` handling for empty patches.
* `0.10.0` added separate pristine verifier execution, sequential commands
  with timeouts, retained raw evidence, patch application in a fresh
  workspace, and atomic trial results. This release remained gated for manual
  testing before merge.

The arc proved that a task could be loaded, prepared, executed, patched,
verified, and explained without conflating agent and verifier environments.

### Scheduling, recovery, reporting, and qualification: 0.11.0 to 0.20.0

The second arc turned the vertical slice into an operable and auditable
system.

* `0.11.0` added deterministic scheduling, stable admission order, explicit
  concurrency and trial limits, immediate successful-attempt banking,
  lifecycle events, and cancellation behavior.
* `0.12.0` added independent build, agent, and verifier phase policies,
  timeout multipliers and overrides, bounded infrastructure retries,
  separate retry identity, and resource-safe release ordering.
* `0.13.0` added event-validated recovery planning, stable resumption of
  unfinished and retryable work, and explicit orphan-resource reporting.
* `0.14.0` added read-only status snapshots, occupancy and ETA calculations,
  graceful stop, bounded waiting, safe cleanup, and durable notifications.
  Its review recorded 69 passing tests.
* `0.15.0` added audit and scoring contracts with explicit missing and invalid
  evidence states and task-equal enterprise scoring.
* `0.16.0` added pure deterministic report rendering and machine-readable
  JSONL output.
* `0.17.0` added the local smoke workflow and artifact/report wiring while
  keeping host qualification separate from fixture validation.
* `0.18.0` added Debian packaging, checksums, SBOM metadata, optional signing,
  version propagation, installation, and rollback wrappers.
* `0.19.0` added host-profile qualification and conservative scale
  recommendations without inventing unavailable benchmark evidence.
* `0.20.0` added repeatable release qualification, release ownership, secret
  scanning, requirement-to-test coverage, package checks, report
  regeneration, and operator documentation.

The `0.20.0` checkpoint explicitly separated source-level and local proof
from external gates requiring customer hardware, Docker, credentials, and
overnight execution. It did not claim those external gates had passed.

### Container execution and qualification hardening: 0.21.0 to 0.23.0

`0.21.0` connected the Docker-backed Copilot and verifier paths and added
realistic smoke layouts, agent image preparation, local environment-image
reuse, lifecycle progress, usage and cost capture, and cancellation handling.

`0.22.0` hardened qualification behavior with structured stage events,
combined agent and verifier duration scheduling, 8 GiB container memory
limits with Docker swap preserved, retained verifier evidence for timeout and
termination paths, bounded whole-trial retries, and agent homes outside
workspaces.

`0.23.0` made preparation concurrency an explicit `P` contract. One
observable preparation gate covered agent and verifier image/workspace
preparation and patch application. Durable gate events recorded waiting,
acquisition, release, cancellation, capacity, occupancy, queue depth, and
wait time. Verifier-only retries reused the exact agent patch and cost while
using a fresh verifier image and pristine workspace. Clean no-patch exits and
agent execution timeouts became terminal instead of rerunning the agent.

The primary controlled K=2 benchmark on z01 used 113 tasks, `N=32`, and
`P=12`:

* 156 of 226 attempts passed, for a 69.03% trial pass rate.
* 95 of 113 tasks passed at least once, for 84.07% task-level coverage.
* Known cost was $78.2231 with 97.35% cost-evidence coverage.
* Cost per costed task attempt was $0.3556.

Failed, retried, timed-out, OOM, and missing-cost attempts remained in the
appropriate denominators.

### Release 0.24.0

Release `0.24.0` consolidated Docker and qualification hardening:

* Agent and verifier containers use explicit create, start, exec, `docker cp`,
  and remove lifecycles instead of host bind mounts.
* Logs, complete patches, verifier evidence, and optional retained
  workspaces are copied out before cleanup.
* Only explicitly registered task-derived images are removed. Base images,
  pulled ECR images, and unrelated tags are preserved.
* Each ECR-backed logical trial builds a uniquely tagged child image from the
  pulled parent.
* Wharf-owned images and containers use `agent-` and `verifier-` prefixes.
* `--shortest-first` provides an opt-in alternative to the default
  longest-estimated-duration-first admission order.
* Versioned Copilot CLI image caching removes repeated npm installation while
  retaining environment and CLI identity in cache keys.
* Portable analytics export, manifest generation, planned-denominator PASS@1,
  cost coverage, task accuracy, duration evidence, and retry coalescing were
  formalized.
* Unfinished retained attempts can be resumed without rerunning completed
  attempts, while retry history and logical-attempt identity remain intact.
* Resume-boundary reconciliation clears stale nonterminal progress records
  while retaining terminal history for counts, pass rates, missing graders,
  and cost.

The mountless Docker validation recorded 170/170 in the final
Docker-backed verifier-capture suite, passing focused ECR-child,
role-prefix, and capture tests, a real retained verifier rerun, formatting,
whitespace, cleanup, and image-preservation checks.

### Release 1.0.0

Wharf 1.0.0 closes the MVP qualification arc. The release accepts the
retained real-run, container-backed smoke, HydraFusion, and unattended K=4
benchmark evidence. The two HydraFusion benchmark exports are preserved in
`data/`, while the complete Wharf and DeepSWE reference job directories remain
available on the execution host for audit and replay.

The final `0.25.0` release-candidate work strengthened the enterprise task
evaluators, added provider-default effort handling, normalized HydraFusion
identity, added `--cli-version` and `WHARF_CLI_VERSION`, and raised enterprise
agent timeouts to 60 minutes. At the 1.0.0 close, portable enterprise analytics
retained 10 K=4 arms and 560 fully scored, fully costed logical trials.

The pre-tag code review found and fixed configuration and recovery gaps that
would have undermined unattended runs:

* resolved credentials now cross the Docker boundary transiently rather than
  being ignored in favor of a machine-specific file;
* run-level setup, agent, and verifier timeout overrides now take precedence
  over task values and defaults;
* terminal results and secret-free run plans are written atomically;
* filtered resume restores exact task identities and effective policy, treats
  malformed results as pending, and keeps dry-run planning read-only;
* Ctrl+C requests cancellation before lifecycle-owned cleanup instead of
  racing global Docker deletion against evidence capture; and
* verifier workspaces and externally cancelled process trees are cleaned on
  success and failure paths.

The final runtime candidate at commit `54c887b` passed 226/226 tests, a Release
build, formatting verification, and Docker-backed smoke with Copilot CLI
1.0.96. The old `1.0.0` tag was removed, and its replacement points to
`1ab71c4`, with `54c887b` as its validated runtime ancestor.

The release is intentionally bounded. Repair and replay workflows, hosted
dashboards, alternate agent products, distributed orchestration, and larger
post-v1 scale profiles remain future work.

### Post-1.0 hardening and benchmark reporting: 1.0.1

The `1.0.1` branch contains three post-tag commits through `5cc3dac`:
CLI version image resolution (`54e1604`), default effort selection
(`4238b56`), and benchmark exports and comparison reports (`5cc3dac`).
This is a branch checkpoint, not evidence of a separate tagged release or
a rerun of the entire MVP acceptance matrix.

#### Container version identity must not depend on the host CLI

Exact CLI versions previously passed through a host-installed CLI probe
before image preparation. A valid version request could therefore fail
because the host executable differed, even though Wharf could build and
validate the requested container image.

The fix makes an exact selected version authoritative without probing the
host. `latest` instead resolves the published `@github/copilot` version from
the npm registry. Image preparation still installs and validates the exact
resolved package version, and registry transport, HTTP, malformed JSON, and
missing-version failures remain explicit rather than falling back to host
state. Flag, environment, manifest, and default precedence is unchanged.

The retained [review](.github/tracking/2026-10-15-cli-version-image-resolution-review.md)
records 20/20 focused adapter/image tests, 229/229 full Release tests,
formatting verification, and Docker-backed smoke that built the CLI 1.0.98
payload image and passed agent and verifier execution.

#### Provider-default effort became the actual configuration default

When no effort is configured, Wharf now selects `default` instead of `max`
and omits the provider effort argument. Explicit flag, environment, and
manifest settings retain their precedence. Configuration tests cover the
default and explicit precedence cases. This is an intentional behavior
change: future runs must retain their effective effort rather than assuming
that an omitted setting reproduces an older maximum-effort run.

#### Benchmark evidence expanded without merging benchmark universes

The new HydraFusion CLI 1.0.98 exports remain separate from both historical
HydraFusion runs and the other benchmark suite. The canonical DeepSWE suite
uses 113 tasks at K=4 for these runs; the enterprise suite uses 14 tasks at
K=4. Their pass rates are not interchangeable.

| Dataset / HydraFusion CLI batch | Passes / planned | PASS@1 | P2P errors | Cost / costed trial | Agent time / attempt |
| --- | ---: | ---: | ---: | ---: | ---: |
| DeepSWE 1.0.95 All-1 | 311/452 | 68.81% | 36 | $1.2074 | 1424 s |
| DeepSWE 1.0.95 All-2 | 317/452 | 70.13% | 32 | $1.1726 | 1466 s |
| DeepSWE 1.0.98 All-1 | 318/452 | 70.35% | 28 | $1.2149 | 1069 s |
| Enterprise 1.0.95 All-1 | 51/56 | 91.07% | 0 | $0.3186 | 303 s |
| Enterprise 1.0.98 All-1 | 54/56 | 96.43% | 0 | $0.4116 | 235 s |
| Enterprise 1.0.96 All-1 | 54/56 | 96.43% | 1 | $0.3197 | 273 s |
| Enterprise 1.0.97 All-1 | 52/56 | 92.86% | 1 | $0.3416 | 316 s |
| Enterprise 1.0.100 All-1 | 53/56 | 94.64% | 1 | $0.2932 | 272 s |
| Enterprise 1.0.101 All-1 | 52/56 | 92.86% | 1 | $0.3527 | 219 s |
| Enterprise 1.0.102 All-1 | 51/56 | 91.07% | 2 | $0.3341 | 267 s |
| Enterprise 1.0.103 All-1 | 51/56 | 91.07% | 2 | $0.3930 | 229 s |

P2P errors count trials with at least one measured pre-existing-test failure,
not individual failing tests. Agent time is the task-weighted mean retained
agent-command duration per attempt, including whole-trial retries, rounded
from unrounded evidence and excluding preparation and verification.

DeepSWE All-2 retains 449 rows but keeps its planned 452-trial denominator.
The new DeepSWE export retains all 452 logical trials, with costs for
450/452 (99.56%) and complete duration coverage. Its known cost is $546.7052;
three whole-trial retries and seven verifier-only retries remain nested
evidence rather than additional rows. All eight enterprise HydraFusion arms
have complete score, test, cost, and timing evidence, with no exception
categories. The later enterprise arms, CLI 1.0.96 through 1.0.103, range from
91.07% to 96.43% PASS@1; the latest 1.0.103 run matches 1.0.102 at 51/56
with two P2P-error trials, at higher cost and lower agent time.

The latest DeepSWE run adds one pass over All-2 while reducing observed
agent time and measured P2P-error trials. The enterprise run adds three
passes with no P2P errors, lower agent time, and higher average cost.
These are observed comparisons, not an isolated causal test of CLI version.
Historical HydraFusion filenames keep their legacy `max` label; the new
exports use `default`.

The reporting work rebuilt the matching manifests, updated ranked tables
sorted by PASS@1, P2P errors, and cost, and added a reproducible
[non-HVE PASS@1 versus cost graph](docs/pass-at-1-vs-cost.svg) with
[portable source data](docs/pass-at-1-vs-cost.csv). Fifteen costed runs are
plotted; Muse is identified without an X coordinate because per-trial cost
evidence is unavailable. Missing costs are never imputed, including from
batch-level bills. The [DeepSWE analytics notes](ANALYTICS.md) and
[enterprise summary](enterprise/data/summary.md) retain the detailed metric
definitions and coverage.

## Testing methodology

Testing evolved from package-level tests into layered boundary and operational
validation.

* Contract tests covered serialization round trips, configuration precedence,
  invalid input, versioning, redaction, event sequencing, task checksums,
  retry classification, scoring states, and missing evidence.
* Boundary tests made host checks, image preparation, Copilot validation,
  agent execution, verifier preparation, scheduling, recovery, status,
  cleanup, analytics export, and report rendering injectable and independently
  testable.
* Docker-backed integration tests exercised isolation, timeout, cancellation,
  OOM, patch capture, ECR parent/child behavior, `docker cp` transfer,
  cleanup ownership, CLI caching, and retained verifier reruns.
* Smoke and qualification tests distinguished deterministic local fixtures,
  Docker-backed smoke, source-level release qualification, and controlled
  real-hardware benchmark evidence.
* Long-running tests and benchmarks ran in named `tmux` sessions so they
  could be monitored without losing execution on an SSH or VS Code
  disconnect.
* Analytics treated one retained logical trial as one row. Infrastructure and
  verifier retries remained nested evidence rather than extra trials.

The analytics contract preserves reward, test counts, task checksum, language,
patch size, exception category, cost, and timing evidence. It keeps missing
costs null, separates trial-level PASS@1 from task-level accuracy, and
requires manifest validation for row count, task coverage, unique trial
identity, K depth, reward totals, cost conversion, and duration coverage.

## Benefits of Sessions and RPI

Sessions reduced context drift, limited the failure radius of a bad frame,
created frequent usable checkpoints, and made scope cuts before implementation
was expensive. Session logs, repository memory, tracking artifacts, and
retrospectives preserved continuity across context resets. The close ritual
also made “code written” distinct from “work released and evidenced.”

RPI prevented investigation and implementation from collapsing into one
activity. Research reduced invented APIs and unsupported assumptions. Plans
made work executable by naming files and exit criteria. Implementation stayed
bounded by an approved approach. Review checked both requirements and code,
then separated test execution status from accepted outcomes. When evidence
invalidated a plan, the workflow could return to Research or Plan instead of
silently continuing.

Together, Sessions and RPI allowed Wharf to move quickly without becoming
opaque. Each release stayed small enough to reason about, major decisions were
recorded, and benchmark or qualification claims were tied to retained
evidence rather than inference.

# Session retrospectives

Append one bullet at the close of every Wharf implementation session. Record
what was surprising and what should be done differently in the next session.

- Session 20 (`0.20.0`): Release qualification must separate repeatable
  source-level gates from infrastructure evidence. Name ownership explicitly,
  fail automated checks loudly, and never convert unavailable signatures or
  overnight measurements into implied passes.
- Session 22 (`0.22.0`): Environment overrides are not sufficient protection
  when project configuration has higher precedence. Keep package-manager homes
  outside project trees, retain hard resource limits as containment, and use
  isolated task reruns to qualify the root-cause fix before another full run.
- Session 25 (`0.25.0`): Turning Wharf into 14 enterprise DeepSWE tasks made
  evaluator quality part of benchmark correctness. Reflection and API-shape
  checks could reward implementations that echoed prompt details without
  proving behavior, so replace them with executable contract tests and keep
  pass-to-pass compatibility checks separate. Validate every task baseline
  and reference patch before exporting results. The seven existing `all-1`
  arms provide 392 K=4 trials with complete cost and timing evidence, but do
  not compare them with the earlier 13/14 result until a controlled rerun uses
  the revised evaluators. Carry task/evaluator revision identity into future
  exports so comparisons cannot silently mix contracts. The new `default`
  effort setting also leaves provider effort selection to the model and is
  recorded in run identity.
- Release candidate (`1.0.0`): The final review showed that accepting,
  validating, and rendering configuration is not proof that runtime execution
  consumes it. Trace every effective setting through the execution boundary,
  persist the exact secret-free resume contract at run creation, and test
  failure-path cleanup rather than only successful completion. The first
  cleanup check also found a preparation-failure workspace leak after the main
  suite was green, so future release closes should include filesystem and
  Docker residue checks before declaring the candidate complete.

- Session 1 (`0.1.0`): Cobra's automatic version flag unexpectedly allowed
  trailing unknown input to succeed, and parsing lower-precedence environment
  values before changed flags weakened the precedence contract. Next session,
  test invalid lower-precedence values and mixed global-flag/argument cases as
  soon as each configuration layer is introduced.
- Release close (`0.1.0`): The independent code review caught integration
  defects that package-level tests missed, especially logger wiring and Cobra
  help error propagation. Keep compiled CLI boundary probes in every release
  proof, not only unit and race tests.
- Migration restart (`0.1.0` withdrawn): Replacing the short-lived Go
  implementation before the first supported release avoids maintaining two
  languages. Keep the .NET process boundary and compiled CLI probes as the
  first proof of the restarted release.
- Session 2 (`0.2.0`): A standalone task prompt produced a clearer enterprise
  boundary than directing the agent through the repository session workflow.
  Keep future test prompts self-contained, while retaining implementation
  evidence separately in `.github/tracking/`.
- Session 3 (`0.3.0`): Custom identifier structs initially compiled but did
  not round-trip through JSON deserialization, which surfaced only when replay
  materialized a summary. Add serialization round-trip tests immediately when
  introducing durable contracts, and prefer explicit converters over relying on
  constructor inference.
- Session 4 (`0.4.0`): Harbor task manifests vary in whether the task-level
  description is populated, while metadata consistently carries the display
  description. Preserve the source shape but use the metadata description as
  the explicit fallback, and keep reference material out of vendored fixtures.
- Session 5 (`0.5.0`): A real host probe exposed that a machine can have a
  healthy Docker daemon while still failing workload capacity, such as a low
  inotify limit. The first implementation also conflated inotify instances
  and watches; validate related kernel settings independently and keep
  platform, daemon, and capacity outcomes separate so an unsupported-host
  override cannot hide a safety failure.
- Session 15 (`0.15.0`): Scoring is safest when every measurement carries an
  explicit state. Keep missing and invalid evidence distinct from measured
  zero, and preserve the documented task-equal weighting in fixtures.
- Session 5 follow-up: WSL2 can run with a custom kernel that removes the
  standard Microsoft markers. Support an explicit, clearly labeled WSL
  classification without weakening any safety probe.
- Session 6 (`0.6.0`): Rate-limit handling needs two distinct forms of
  evidence: append each 429 immediately so interruption cannot erase it, then
  atomically refresh the per-image aggregate with the eventual outcome.
  Continue injecting delay and randomness at the boundary, and keep registry
  pressure inside the preparation service so Session 7 can place the whole
  pipeline behind one `B` gate without inventing a second semaphore.
- Session 7 (`0.7.0`): A shared build task must outlive an individual caller's
  cancellation; otherwise one cancelled waiter can poison every duplicate
  request. Keep cancellation at the wait boundary and remove failed
  operations from the in-flight map so recovery is explicit and testable.
- Session 8 (`0.8.0`): Secret safety is easiest to preserve when credential
  values never enter the adapter's data model at all. Pass source metadata,
  validate it at the boundary, and make redacted command output structurally
  incapable of containing the credential.
- Session 9 (`0.9.0`): Isolation is clearest when mount validation happens
  before any runner is invoked. Keep fake-agent tests at that boundary, and
  persist an unfinished result before rejecting an empty patch so debugging
  evidence survives failed execution.
- Session 10 (`0.10.0`): Keeping workspace creation, patch application, and
  verifier execution injectable makes the single-task proof testable without
  pretending that a local unit test is a full Docker integration. Preserve
  the manual release gate until the real environment is exercised.
- Session 11 (`0.11.0`): Logical-attempt banking belongs at admission/result
  boundaries, not in a later aggregate pass. Stable ordering and immediate
  banking make duplicate suppression and cancellation behavior observable.
- Session 12 (`0.12.0`): Resource release must happen before retry evidence is
  written; otherwise a slow telemetry sink can falsely hold a phase slot and
  undermine work-conserving behavior. Keep infrastructure retry identity
  separate from logical-attempt identity.
- Session 13 (`0.13.0`): Recovery is safest as a pure plan over validated
  events plus an explicit live-resource snapshot. Report orphan resources
  separately so resume never hides cleanup or mutates retained evidence.
- Session 14 (`0.14.0`): Cleanup safety is easiest to prove by making
  ephemeral-resource IDs an explicit input and never deriving them from the
  retained evidence tree. Keep status as a pure projection so operators can
  inspect runs without changing recovery state.
- Session 16 (`0.16.0`): Keeping report rendering pure over a versioned
  data-only input makes deterministic regeneration straightforward. Keep CLI
  file handling thin and test the exact JSONL line shape separately.
- Session 17 (`0.17.0`, unreleased): A local fixture can prove artifact and
  report wiring without pretending that host qualification passed. Keep
  platform overrides separate from Docker and capacity gates.
- Session 18 (`0.18.0`): Packaging needs explicit unsigned labeling and a
  version override that is shared by the published executable and Debian
  metadata. Keep host mutation isolated behind small sudo wrappers.
- Session 19 (`0.19.0`): Scale qualification must not invent benchmark
  evidence. Keep hardware, deadlines, losses, and score completeness as
  explicit retained inputs, and cap every recommendation by operator limits.
- Post-1.0 checkpoint (`1.0.1`): Version selection must describe the container
  that executes the agent, not an unrelated host installation. Probe mutable
  aliases at their authoritative source, keep exact versions deterministic,
  and validate the installed image payload. Reporting also needs explicit
  dataset, effort, cost-coverage, and denominator contracts: similarly named
  DeepSWE and enterprise runs are not interchangeable. Preserve separate
  exports, distinguish unknown measurements from zero, round only at
  presentation, and do not attribute benchmark deltas to a CLI change without
  a controlled comparison.
