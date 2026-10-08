# Project Guidelines

## Benchmark execution

Always run long-running tests and benchmark jobs inside a named `tmux` session so
they survive SSH or VS Code disconnects and can be monitored or reattached.
Run the job in the foreground with stdout and stderr connected to the tmux pane;
do not wrap it in shell redirection, `nohup`, backgrounding, or an outer logging
pipeline. Follow [RUN_MONITORING.md](../RUN_MONITORING.md) for tmux lifecycle,
read-only monitoring, and provider-specific checks.

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

The controlled benchmark decision made before the next result excludes All-1
from comparative scoring because it predates the finalized memory, OOM, retry,
patch-capture, failed-agent verification, and cost contracts. Retain its
evidence unchanged as pre-contract qualification history; do not delete it.
All-2 remains transitional and All-3 remains supporting `0.22.0` evidence.
The next primary benchmark is a fresh z01 K=2/N=32/P=12 run on the pushed
`0.23.0` code. Its primary metrics are passes divided by 226 and USD cost per
task attempt, with cost coverage reported separately. Include every OOM,
timeout, retry, and verifier result; do not add post-result exclusions.
