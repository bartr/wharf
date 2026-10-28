# Project Guidelines

## Benchmark execution

Always run long-running tests and benchmark jobs inside a named `tmux` session so
they survive SSH or VS Code disconnects and can be monitored or reattached.
Run the job in the foreground with stdout and stderr connected to the tmux pane;
do not wrap it in shell redirection, `nohup`, backgrounding, or an outer logging
pipeline. Follow [RUN_MONITORING.md](../RUN_MONITORING.md) for tmux lifecycle,
read-only monitoring, and provider-specific checks.

## Scoring completed runs

Use the canonical DeepSWE analytics contract in `../deep-swe/ANALYTICS.md` and
`../deep-swe/data/README.md`. Export completed Wharf runs to
`../deep-swe/data/<arm>.jsonl`, with one JSON object per logical trial. Do not
emit infrastructure or verifier retries as additional trials.

Report DeepSWE `PASS@1` as the trial-level pass rate: passing trials divided by
all retained logical trials. Count failed, errored, timed-out, infrastructure-
failed, and cancelled trials in the denominator. Do not label task-level
`passed at least once`, `passed every attempt`, or majority-pass counts as
`PASS@1`. Task accuracy is a separate metric and considers a task solved when
any retained trial has `reward > 0`.

Preserve each trial's reward, functional and regression test counts, task
checksum, language, patch size, exception category, cost, and timing evidence.
Use actual agent-command duration for `agent_seconds`, summed across whole-trial
retries, and exclude preparation and evidence-copy time. Use the elapsed span
from the first preparation start through final cleanup for `trial_seconds`.

Convert Copilot AI Credits to `cost_usd` at one cent per credit. Keep missing
costs null, report cost-evidence coverage, and do not impute missing values.
Compute cost per costed trial from trials with retained cost evidence. Keep
aggregate task cost and cost per pass separate from that primary metric.

After exporting an arm, rebuild `../deep-swe/data/manifest.json` with
`python3 ../deep-swe/scripts/build_data_manifest.py`. Validate row count,
unique logical-trial identity, task coverage, configured K depth, reward totals,
cost conversion, duration coverage, and the generated manifest entry before
reporting a score.

Wharf documentation lives under `docs/`. Session RPI artifacts belong under
`.github/tracking/`; keep those artifacts with the repository so decisions and
validation evidence survive context resets.

Session 2 established strict `1.0` JSON run manifests and an explicit
flag-over-environment-over-manifest-over-default precedence contract.
`wharf run --manifest <path> --dry-run` validates and renders a deterministic
plan without creating output directories, run state, Docker resources, or
network activity. Manifest and `WHARF_API_KEY`, `WHARF_TOKEN`, and
`WHARF_PASSWORD` values are redacted in rendered output. Keep configuration
validation before any output-directory creation.

Session 3 established a filesystem-backed `1.0` state contract. Each run
stores its effective configuration, an append-only `events.jsonl` stream, and
an atomically replaced `summary.json`. Event IDs are idempotent, sequence
numbers are contiguous, malformed or mismatched records fail explicitly, and
logical attempts must remain distinct from infrastructure retries. Keep
read-only summary access independent from the per-run writer lock.

Session 4 established Harbor task loading as a library boundary. Schema `1.3`
manifests are parsed strictly, task checksums use sorted relative paths and
content, and selection is deterministic across language, include, exclude,
shard, seeded-sample, and K-attempt expansion options. Vendored fixtures under
`tests/fixtures/harbor/` are sanitized: never add solutions, hidden tests,
reference patches, or grader-only material to the Wharf checkout.

Session 5 adds read-only host and Docker preflight through `wharf check`.
Supported platform classification is Ubuntu 24.04 amd64, including WSL2.
`--allow-unsupported-host` may override only that classification; Docker
reachability and capacity failures remain blocking. Do not make preflight pull
images, create Docker resources, or mutate daemon configuration.
The current development host `z01` is native Ubuntu, not WSL; do not set
`WHARF_WSL` there. The explicit `WHARF_WSL=1` classification remains available
only for a genuinely WSL2 environment whose custom kernel hides standard
markers, and it does not bypass safety checks.

Session 6 adds image preparation through `Wharf.Image`. Inspect the local image
store before registry access, coalesce duplicate references for the coordinator
lifetime, and retry only explicit HTTP 429 or registry rate-limit failures.
Backoff is capped exponential with injected jitter; registry pressure grows on
429 responses and decays one step after success. Durable schema `1.0` evidence
uses `image-pulls.jsonl` plus atomically replaced
`image-pull-summary.json`. Default console output aggregates affected images;
verbosity 2 and 3 expose each retry. Keep the complete `B` gate around pull,
context preparation, and derived build as Session 7 work rather than adding a
separate pull semaphore or public pull-concurrency setting.

Session 7 adds `DerivedImageCoordinator`. It applies one `B` gate across base
image preparation, context preparation, and derived build. Deterministic
SHA-256 keys include the base image, ordered scripts, sorted versions, and
sorted configuration. Normal identical builds coalesce and successful results
are reused; `ForceBuild` bypasses the cache. Caller cancellation only cancels
that caller's wait, and failed in-flight operations are removed for retry.

Session 8 adds the `Wharf.Copilot` adapter boundary. It requires exact
requested/effective three-part CLI version equality, records native and
normalized frontier effort, and allows only explicitly allowlisted HTTPS
endpoints. Environment, file, and stdin credential sources retain source
metadata only; credential values must never enter arguments, environment
configuration, or redacted command output.

Session 9 adds the `Wharf.Agent` execution boundary. Agent source and
workspace directories must be distinct absolute mounts, hidden verifier paths
must not be inside either mount, and Docker runs as UID `1000:1000` on an
explicit network. Persist stdout, stderr, trajectory, patch, timing, usage,
and a versioned result summary. An empty patch is `unfinished`, not success,
and all evidence must be retained before reporting the error.

Session 10 adds the `Wharf.Verifier` boundary. Copy task source into a fresh
workspace before applying a retained patch; never reuse the agent workspace.
Run verifier commands sequentially with explicit timeouts, retain raw
stdout/stderr and the patch, and atomically write a versioned trial result.
Manual testing must approve the unmerged `0.10.0` branch before release.

Session 11 adds the `Wharf.Scheduler` boundary. Attempts are admitted in
stable attempt-ID order under explicit concurrency and trial limits.
Successful logical-attempt IDs are banked immediately, duplicate attempts are
skipped, lifecycle transitions are emitted through an event sink, and
cancellation prevents new admissions while marking pending work cancelled.

Session 12 adds `Wharf.Execution` phase policies. Build, agent, and verifier
gates are independent; timeouts combine phase defaults, multipliers, and
overrides. Retryable infrastructure failures have bounded retries and
separate retry IDs/evidence, while excluded failures surface immediately
without changing logical-attempt accounting.

Session 13 adds `Wharf.Recovery`. Resume planning replays and validates
retained events, excludes completed and deterministic-failure logical
attempts, resumes unfinished and explicitly retryable work in stable order,
preserves immutable retry counts/IDs, and reports orphaned live resources
without silently deleting evidence.

Session 14 adds `Wharf.Operations`. Status snapshots are read-only and derive
counts, occupancy, retries, and ETA from retained events. Graceful stop
cancels first, waits within a bound, and cleans only explicitly ephemeral
resources. Completion and blocked notifications are durable; webhooks require
an explicit HTTPS endpoint.

Session 22 releases the `0.22.0` qualification hardening. Structured stage
events drive reconciled progress occupancy and active-stage timing. Agent and
verifier containers keep an 8 GiB memory limit without disabling Docker's
default swap allowance. Scheduling uses combined historical agent and verifier
durations. Verifier timeouts and exits 137/143 retain evidence before entering
bounded whole-trial retry. Agent homes are retained as sibling directories
mounted at `/home/wharf`, never below `/workspace`, so project-level pnpm
version management cannot recursively rediscover its own staging tree. The
next release replaces public `B` build concurrency with `P` preparation
concurrency and applies the shared gate to both agent and verifier preparation,
but not verifier execution.

Session 23 starts `0.23.0` with the breaking `P` preparation-concurrency
contract. Only `p`, `--p`, `P`, and `WHARF_P` are accepted; no B compatibility
aliases remain. One observable gate covers agent image/workspace/Copilot
preparation, agent retry preparation, verifier image preparation, pristine
workspace creation, and patch application. Release the permit before agent or
verifier commands execute. Durable `preparation-gate` events record ordered
waiting, acquired, released, and cancelled transitions with capacity,
occupancy, queue depth, operation type, and acquisition wait time.

Verifier timeouts and externally terminated verifier commands retry only the
verifier boundary. Preserve the exact agent patch and cost, archive failed
verifier evidence under `verifier-retries/retry-N`, and use a fresh verifier
image identity and pristine workspace for the retry. Agent-side typed failures
other than `AgentTimeout` and `AgentUnfinished` continue to use whole-trial
retry under `trial-retries/retry-N`. A clean exit with no patch and an agent
execution timeout are terminal and must not rerun the agent. Ordinary
reward-zero verification is not retryable.

The controlled benchmark decision made before the next result excludes All-1
from comparative scoring because it predates the finalized memory, OOM, retry,
patch-capture, failed-agent verification, and cost contracts. Retain its
evidence unchanged as pre-contract qualification history; do not delete it.
All-2 remains transitional and All-3 remains supporting `0.22.0` evidence.
The primary controlled z01 K=2/N=32/P=12 benchmark on commit `af3574f` passed
156/226 attempts (69.03%). Attempt 1 passed 80/113 and attempt 2 passed 76/113;
95/113 tasks passed at least once. Known cost was $78.2231 across 220 attempts,
or $0.3556 per costed task attempt with 97.35% coverage. Include all four
whole-trial retries, four OOM events, two agent timeouts, and every verifier
result; do not add post-result exclusions. This run predates verifier-only
retry commit `fcc8235` and therefore reran the agent for verifier failures.

The `docker` spike branch replaces host bind mounts for both agent and verifier
containers with explicit create/start/exec/`docker cp`/remove lifecycles.
Agent logs and the complete binary patch are created inside the container and
copied into trial evidence before removal; `--retain-workspaces` copies the
full workspace out. Verifiers receive retained artifacts through `docker cp`
and copy `/logs/verifier` back before cleanup. Remove explicitly registered
task-derived agent and verifier images after their logical trial, but never
remove base images, pulled ECR images, or unrelated tags through broad name
matching.
Every ECR-backed logical trial must build its own uniquely tagged child image
from the pulled parent. Never run multiple logical trials directly from the
shared ECR tag, and register only the child image for deletion.
Prefix Wharf-owned agent image tags with `agent-` and verifier image tags with
`verifier-` so operators can list managed images without matching pulled ECR
images. Prefix Wharf-owned agent container names with `agent-` and verifier
container names with `verifier-` so `docker ps` exposes resource ownership.

Run admission remains longest-estimated-duration first by default within each K
wave. The opt-in `--shortest-first` flag reverses duration ordering within each
wave to produce early results sooner while preserving deterministic task and
attempt tie-breakers.

The 1.0.0 runtime candidate is commit `54c887b` on branch `0.25.0`. The old
`1.0.0` tag was removed locally and from origin; do not recreate it on an older
commit. The candidate passed 226/226 tests, a Release build, formatting
verification, and Docker-backed smoke with Copilot CLI 1.0.96. Commit this
release-close memory, then create and push the replacement `1.0.0` tag at that
new HEAD with `54c887b` as its validated runtime ancestor.

Copilot CLI selection follows
`--cli-version` over `WHARF_CLI_VERSION` over manifest `cliVersion` over
`latest`. Exact requested/effective version equality remains mandatory. Run
phase timeout overrides take precedence over task timeouts, which take
precedence over built-in defaults; `reportSeconds` is rejected because the
current pipeline has no report phase.

Every new run atomically retains a secret-free `run-plan.json` containing the
exact selected task IDs, checksums, and effective configuration. Resume must
restore that task subset and immutable policy, validate terminal result
identity, leave planning and `--dry-run` read-only, and archive malformed
terminal evidence only when execution resumes. Terminal `result.json` writes
remain atomic.

Ctrl+C requests cancellation only so agent and verifier lifecycles can retain
evidence before cleanup. Never restore generic container-name cleanup; explicit
orphan cleanup may select only Wharf ownership labels. File-backed verifier
workspaces must be deleted after verification and preparation failures, and
external host-verifier cancellation must kill and reap the complete process
tree.

Keep enterprise analytics in `enterprise/data/` separate from the 113-task
DeepSWE controlled benchmark dataset. After exporting enterprise trials,
rebuild `enterprise/data/manifest.json` with `wharf analytics manifest
--data-directory enterprise/data`. Each manifest arm records model, effort,
CLI, CLI version, passes, P2P errors, score, cost per costed trial, and average agent
time, alongside trial/task totals and cost coverage. Keep
`enterprise/data/summary.md` aligned with the manifest, displaying model,
effort, CLI, CLI version, passes and PASS@1, P2P errors, score, cost per trial, and
average agent time. PASS@1 is passing trials divided by all retained logical
trials. Do not track average cost per task or mean trial time in enterprise
manifests or comparison summaries; retain the underlying per-trial evidence.
Cost per trial excludes trials without retained cost evidence; average
agent time uses retained agent-command durations. Compute
`score = passes / trials - (p2p_errors * 0.0025)`: subtract 0.25 percentage
points per trial with a P2P error, not per failed test. Store score as a
fraction, display it as a percentage, and leave negative values unclamped;
empty arms have a null score. Keep score separate from PASS@1 and the
Enterprise Score audit metric. Do not mix enterprise runs
into DeepSWE analytics or rebuild the DeepSWE manifest for enterprise changes.

Release packages must include the complete .NET publish output, not just the
apphost executable. Qualification must extract the Debian package and execute
its `usr/bin/wharf --version`, checking the requested version. Checksum, SBOM,
and package metadata checks alone do not prove the packaged program can run.
Retain failed package/run evidence and record the failure under
`.github/tracking/` before replacing or deleting it.
