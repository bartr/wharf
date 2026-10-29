# Wharf development, testing, and operations summary through 1.1.0

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
effort the default, and extended the retained benchmark comparisons. Release
1.1.0 brings those changes together with updated CLI conventions, richer
analytics manifests, and an executable-package qualification gate.

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
spanning an overnight window. Through the `1.0.1` branch at `e6fe024`, the
enterprise dataset contains 24 arms and 1344 logical trials with complete
score, cost, and duration coverage. The `1.0.0` tag points to `1ab71c4`, and
no `1.0.1` tag is present at this review. Post-v1 repair/replay, hosted
operations, additional agent products, and multi-host distributed execution
remain outside the delivered scope. Single-host concurrency is different:
post-1.0 operation has exercised `N=32 P=12` as the z01 operating setting,
enterprise runs at `N=64 P=16`, and `N` up to about 2.5 times the processor
count, as described in
[Operating at scale after 1.0.0](#operating-at-scale-after-100). These are
operator-validated settings, not formally qualified scale profiles with
published host-sizing recommendations.

Since 1.0.0, sustained production use has become its own evidence stream. The
operating sections below cover the cold-run preparation-concurrency lessons
that made `P` essential, the 14-task .NET enterprise benchmark built with
Wharf, and a ten-version HydraFusion CLI series that made the team an early,
credible HydraFusion tester.

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

#### Operator observation: challenge confident infrastructure advice

Early in development, the LLM proposed sharing Docker volumes between the
host and containers. The operator questioned the approach, but the LLM
continued to recommend it. In later runs, many upstream task images ran as
root, and the shared-volume workflow encountered repeated permission
problems. After Wharf moved to Docker-managed images and explicit artifact
transfer with `docker cp`, the operator observed that the permission errors
disappeared and caching improved substantially.

This is an operator account, not a result reconstructed from retained
telemetry. The record does not establish which individual change caused the
caching improvement, nor does the operator know exactly what prompted the
initial concern. The useful lesson is that the concern was worth turning
into a test: a plausible, confidently defended recommendation still needs
to be evaluated against the actual users, images, ownership model, and
workload.

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

The release was intentionally bounded. At the 1.0.0 close, repair and replay
workflows, hosted dashboards, alternate agent products, distributed
orchestration, and larger post-v1 scale profiles were future work. Post-1.0
operation has since exercised larger single-host concurrency settings; see
[Operating at scale after 1.0.0](#operating-at-scale-after-100).

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

Later `1.0.1` branch work made HydraFusion with model-selected effort the
default (`215c685`), adopted single-dash concurrency flags with model and
effort aliases (`de47b92`), and added run metadata plus a regression-adjusted
score to the analytics manifest (`7d1cdbf`).

### Release 1.1.0

The accumulated post-1.0 work is released as 1.1.0; the intervening 1.0.1
branch was not tagged. Release notes explicitly call out removed double-dash
concurrency flags and changed model/effort defaults. Fresh qualification
passed 246/246 Release tests, formatting, builds, and Docker-backed .NET smoke
with HydraFusion and Copilot CLI 1.0.105.

Executing the extracted Debian package caught a defect that checksum and
metadata checks had missed: only the .NET apphost was packaged, leaving its
DLL and runtime files absent. The package now includes the full publish
output, and both regression tests and qualification execute the extracted
artifact and check its version. The failed artifact and failure record were
retained before replacement. See the
[1.1.0 qualification](docs/release-qualification.md#110-checkpoint-2026-10-28).

## Operating at scale after 1.0.0

### Preparation concurrency is the stability control

Before preparation had its own gate, large runs failed with Docker write
errors that terminated the entire run, often close to an hour in. Cold runs
were the worst case because every task needed image pulls, derived-image
builds, CLI payload setup, and workspace creation at the same time, but warm
runs could also fail. The operating conclusion is that trial concurrency
(`N`) and preparation concurrency (`P`) are different resources: `N` is mostly
bounded by CPU and memory while agents run, whereas `P` protects the Docker
daemon, storage driver, and disk from bursts of preparation I/O.

On the 16-core z01 host, the operating setting is `N=32 P=12`. The limit is a
combination of processor and disk pressure. A higher `P` is possible on warm
runs, but saves only about a minute there, so the cold-run value is used for
every run rather than trading a small warm-run gain for the risk of losing
hours of benchmark work. Operators have also run `N` up to about 2.5 times the
processor count; the CPU stays pegged for hours, yet Wharf completes without
throwing errors.

Retained preparation-gate telemetry shows the cost of this safety margin is
seconds, not hours:

| Runs | Setting | Peak active / queued | Median wait | Worst wait |
| --- | --- | ---: | ---: | ---: |
| DeepSWE HydraFusion 1.0.98, 113 tasks, K=4, 918 acquisitions, 6.0 h | `N=32 P=12` | 12 / 20 | 0 s | 30 s |
| Enterprise HydraFusion 1.0.98, 1.0.101, 1.0.103-1.0.105, 112 acquisitions each | `N=32 P=12` | 12 / 20 | 0 s | 27 s |
| Enterprise runs at `N=64 P=16`, 112 acquisitions each | `N=64 P=16` | 16 / 40 | 0 s | 45 s |

The gate reached capacity and built a queue in every measured run, which is
what it is for: it absorbs the burst instead of passing it to Docker. The
enterprise runs contain only 56 trials, so `N=64` cannot exceed 56 concurrent
trials there. The retained telemetry measures queueing; the earlier Docker
write-error failures are operator observations from runs before the shared
gate and are not reconstructed from these logs.

### Failed runs need durable records before cleanup

A failed benchmark run is still an experiment with useful results. In
practice, failed job directories were often deleted because the run had
failed, removing the only detailed evidence of what happened. That left
later analysis dependent on operator recollection. When preparing this
retrospective, the LLM had to ask about the importance of `P` because the
failed-run evidence that would have explained the decision was no longer
available.

For future runs, record the run information and results in
[`.github/tracking/`](.github/tracking/) before deleting a failed job
directory. The record should include:

* Run identity, date, source commit, Wharf and Copilot CLI versions, model,
  dataset/task revision, effective configuration, and `K`, `N`, and `P`.
* Host and resource details, cold/warm image-cache state, elapsed time,
  completed and failed trial counts, and available cost coverage.
* The observed failure, exact errors, relevant log excerpts and resource or
  preparation-gate telemetry, and any evidence retained elsewhere.
* The diagnosis, clearly separating observations from hypotheses, the change
  made, and the outcome of any follow-up run.

Keep the record secret-free and preserve the minimal supporting evidence
needed to substantiate its conclusions. A summary does not replace raw
evidence required for scoring or audit; identify anything deleted or missing
explicitly. This makes failed runs durable engineering knowledge rather than
discarded work, gives future LLM sessions better evidence, and avoids having
to reconstruct decisions such as preparation-concurrency limits from memory.

### Enterprise benchmark: .NET coverage at a fraction of the cost

The project used Wharf's own release history to build `fe-labs/enterprise-1.0`,
a 14-task C# benchmark under `enterprise/tasks/`. Each task reconstructs a
real Wharf release boundary, including the scheduler, recovery, verifier,
Copilot adapter, image preparation, packaging, and reporting, from a pinned
base commit. Agents run without network access under a 60-minute timeout, and
separate verifiers apply the retained patch in a fresh environment.

The suite fills two gaps in DeepSWE:

* It adds .NET and enterprise-style service coverage that the 113-task
  DeepSWE suite does not provide.
* It is much cheaper and faster to repeat. A K=4 enterprise arm is 56 trials;
  the latest HydraFusion arm cost $15.01 and completed in about 9.5 minutes of
  run time. The comparable DeepSWE HydraFusion 1.0.98 K=4 arm was 452 trials,
  cost $546.71, and took about 6 hours.

Building the suite was its own engineering effort. Reflection and API-shape
checks were replaced with executable contract tests, pass-to-pass regression
checks were kept separate, and every baseline and reference patch was
validated before results were exported. The two suites remain separate
datasets with separate manifests; their pass rates are not interchangeable.
The enterprise suite is a fast regression and comparison signal, while
DeepSWE remains the broader, more expensive benchmark.

### HydraFusion CLI series

The low cost of the enterprise suite made it practical to test each new
Copilot CLI release with HydraFusion as it shipped. Wharf has produced ten
enterprise HydraFusion arms, 560 logical trials at a total cost of $192.38,
plus three DeepSWE HydraFusion arms.

| CLI | Passes | PASS@1 | P2P errors | Cost / trial | Agent time |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1.0.95 | 51/56 | 91.07% | 0 | $0.3186 | 302.9 s |
| 1.0.96 | 54/56 | 96.43% | 1 | $0.3197 | 272.5 s |
| 1.0.97 | 52/56 | 92.86% | 1 | $0.3416 | 316.4 s |
| 1.0.98 | 54/56 | 96.43% | 0 | $0.4116 | 234.8 s |
| 1.0.100 | 53/56 | 94.64% | 1 | $0.2932 | 272.0 s |
| 1.0.101 | 52/56 | 92.86% | 1 | $0.3527 | 218.9 s |
| 1.0.102 | 51/56 | 91.07% | 2 | $0.3341 | 266.9 s |
| 1.0.103 | 51/56 | 91.07% | 2 | $0.3930 | 229.4 s |
| 1.0.104 | 51/56 | 91.07% | 3 | $0.4028 | 196.7 s |
| 1.0.105 | 52/56 | 92.86% | 2 | $0.2681 | 188.7 s |

PASS@1 stayed between 91.07% and 96.43%. Mean agent time fell from roughly
300 seconds on early versions to 189 seconds on 1.0.105, and 1.0.105 had the
lowest cost per trial in the series. These are observed version-to-version
comparisons rather than controlled causal tests, because model behavior and
service conditions can also change between runs.

This sustained, evidence-backed testing gave the team immediate credibility
with the GitHub team; its VP said he "wants to be our new best friend."
Several joint customer engagements are being planned, but none had started
at this review.

### Microsoft Copilot harness announcement

On 2026-10-28, Microsoft announced the
[Microsoft Copilot harness](https://blogs.microsoft.com/blog/2026/10/28/introducing-the-microsoft-copilot-harness-unlocking-the-potential-of-truly-multi-model-agentic-work/),
the GitHub-built, multi-model agent harness that Microsoft has adopted
company-wide. It powers Copilot Code, Copilot Studio, the GitHub Copilot app,
the CLI, VS Code, and more, and is now available to developers and enterprise
customers through
[Microsoft Foundry](https://devblogs.microsoft.com/foundry/microsoft-copilot-harness-in-foundry)
and the [Copilot SDK](https://github.com/github/copilot-sdk). The
announcement credits the same GitHub engineers behind
[HydraFusion multi-model orchestration](https://github.blog/ai-and-ml/github-copilot/project-hydrafusion-frontier-quality-via-multi-model-orchestration/)
and says those technologies are only possible because GitHub built its own
harness. It positions routing work to "the right model or models based on the
task" as a reason not to build and evaluate a private harness or model router.

Wharf has been evaluating HydraFusion as it prepares to ship. The announcement
raises the importance of that work:

* **The evaluated surface now has a much wider reach.** Wharf tests HydraFusion
  through the Copilot CLI. That CLI runs on the harness Microsoft is now
  standardizing on and offering to customers, so findings about it are relevant
  far beyond one developer tool.
* **Customers are being asked to trust routing they do not control.** The
  announcement invites customers to rely on Microsoft to select models. Wharf
  provides independent, customer-controlled evidence on whether that delegation
  holds up in score, regression safety, cost, and time.
* **Harness improvements need version-level checks.** Microsoft says harness
  improvements benefit every surface it touches. Wharf's per-CLI-release
  HydraFusion series is the mechanism for checking that claim and for detecting
  regressions, such as the increase in P2P-error trials from 0-1 to 2-3 across
  later versions, before customers do.
* **Enterprise .NET evidence complements GitHub's benchmarks.** GitHub
  publishes harness results on [benchmarks.github.com](https://benchmarks.github.com).
  The 14-task C# enterprise suite and the 113-task DeepSWE suite add
  customer-style workloads with retained, auditable evidence.

These implications are this project's assessment, not claims from the
announcement. The announcement does not cite Wharf or its results.

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

- Release 1.1.0: A package can pass checksum, SBOM, and version-metadata checks
  yet be impossible to execute. Validate the extracted deliverable, not just
  its packaging structure. Retain failed artifacts and record the failure
  before replacing them; the missing-DLL discovery immediately demonstrated
  the value of the failed-run documentation lesson.
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
- Post-1.0 operations (`1.0.1`): The failures that matter most at scale
  appeared only in long, cold, real runs, close to an hour in, and lost the
  whole run. Size preparation concurrency for the cold case and use it for
  every run; the measured cost is under a minute of queueing, while the
  avoided risk is hours of lost benchmark work. A cheap, purpose-built suite
  turned version-by-version testing from an occasional event into a routine
  that produced credible external evidence.
