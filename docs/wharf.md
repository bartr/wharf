# Wharf Requirements

Wharf is a simple, scalable, enterprise-focused CLI for running DeepSWE and
enterprise benchmarks from Harbor task definitions.

## Users

- the primary user is an enterprise evaluation engineer operating a customer-owned Ubuntu node
- engineering leaders consume the reports and model comparisons
- benchmark authors provide Harbor-compatible task suites
- source code, hidden tests, patches, credentials, and results remain under customer control

## Product goals

- Keep the common workflow simple: provision, check, smoke, run, resume, report.
- Run one complete, trustworthy evaluation overnight without operator attention.
- Scale from a 32-concurrent on-prem server to large cloud VMs with 88 or more concurrent trials.
- Run on customer-owned infrastructure so source, tests, patches, and results remain under customer control.
- Be cloud- and location-agnostic, but intentionally not Linux-distribution-agnostic.
- Preserve proven Harbor task behavior and local DeepSWE execution enhancements without runtime monkey patches.
- use the DeepSWE K=4 suite as the reference workload for scale and overnight qualification
- publish validated host profiles for 32-concurrent and 88-concurrent runs
- a qualified overnight run must complete before the documented deadline with no completed trials lost

## Release scope and phases

- every requirement in the PRD and LLM requirements file must be assigned a must, should, or could priority
- only must requirements block the v1 release
- v1 must include
  - Harbor-compatible local execution
  - DeepSWE as the initial task provider
  - Copilot CLI adapter
  - check and smoke validation
  - run, resume, status, and stop lifecycle support
  - durable patches, results, trajectories, cost, and test evidence
  - PASS@1 and Enterprise Score reporting
  - static human-readable reports and machine-readable exports
  - the Ubuntu bootstrap process
- automated repair and replay are the next phase after v1 execution parity and reporting are stable
- v1.1 adds Claude Code and Codex adapters
- The canonical Wharf implementation is .NET; the DeepSWE suite remains the
  initial task provider and future enterprise suites are added through the same
  provider boundaries

## Post-MVP execution and scale requirements

The MVP may install the requested Copilot CLI inside each temporary agent
container and reuse the task's configured environment image. After MVP,
`wharf build` must support the Harbor-compatible derived-image workflow:

- Build a local, version-pinned agent image from each task environment image.
- Install the requested Copilot CLI and its runtime dependencies in the
  derived image rather than during every agent startup. Retain one local
  `wharf-copilot-cli` payload image per exact CLI version and platform, then
  compose it onto each configured task image with a BuildKit `COPY --link`
  layer. Retain the composed `wharf-agent-cache` image across logical trials
  and future runs.
- For the mountless Docker spike, use the derived image only for its logical
  trial and remove it after trial evidence is complete. Preserve the configured
  base image, every pulled ECR environment image, versioned CLI payload images,
  and composed agent cache images. Register only uniquely named trial children
  for automatic cleanup.
- Create agent containers without host bind mounts or named volumes. Initialize
  `/workspace`, `/home/wharf`, and `/logs/agent` inside the container, then
  assign them to UID/GID `1000:1000`. Run Copilot and all task commands as that
  user. Capture stdout, stderr, trajectory, Copilot logs, and the complete
  binary patch inside the container and copy them into the run directory with
  `docker cp` before removing the container. Copy the full workspace only when
  `--retain-workspaces` is effective.
- Create verifier containers without host mounts. Copy retained artifacts into
  `/logs/artifacts`, run the verifier, and copy `/logs/verifier` back into the
  run directory before removing the container.
- Prefix Wharf-owned agent container names with `agent-` and verifier
  container names with `verifier-` so `docker ps` exposes resource ownership.
- Limit each agent and verifier container to 8 GiB of memory while leaving
  Docker's default swap allowance in effect, matching Harbor behavior. Agent
  containers also have a 1024-process limit. Disable pnpm's automatic
  package-manager version switching in the agent environment as defense in
  depth; project-level pnpm configuration can override the environment, so the
  separate home path is the primary protection against recursive self-install
  staging.
- Monitor Docker OOM events for each agent. On the first cgroup OOM kill, stop
  that container, retain its evidence, and fail the trial with the explicit
  `OutOfMemory` error type instead of allowing repeated child-process kills.
- Enforce independent agent-image build, agent execution, verifier-image build,
  and verifier execution timeouts. Effective run phase overrides take
  precedence over task timeouts, which take precedence over built-in defaults.
  Timed-out processes and containers are terminated, and agent and build
  timeouts are retained as explicit `AgentTimeout`, `AgentBuildTimeout`, or
  `VerifierBuildTimeout` failures. Retain verifier command evidence before surfacing
  `VerifierTimeout`; classify externally terminated verifier containers as
  `VerifierInfrastructure`. Both are eligible for bounded verifier-only retry
  against the retained patch, while an ordinary reward-zero verifier remains a
  failed test.
- Pass task instructions as an attached `--prompt=<text>` argument so a prompt
  beginning with a dash cannot be parsed as another CLI option.
- Retry a Copilot HTTP 400 reported as an internal server error once in a fresh
  workspace. Retain the failed execution and a separate infrastructure-retry
  record, and include both executions in the task's reported cost.
- By default, retry a whole trial once when an agent execution error does not
  produce a passing patch. Archive the failed execution under
  `trial-retries/retry-1`, then use fresh agent workspace and container
  identities without incrementing K. Do not retry an agent failure whose
  retained patch passes. A clean agent exit with no patch is terminal
  `unfinished`, and an agent execution timeout is terminal `AgentTimeout`;
  neither reruns the agent.
- Retry verifier timeouts and externally terminated verifier commands against
  the exact retained agent patch without rerunning the agent. Archive failed
  verifier evidence under `verifier-retries/retry-1`, then use a fresh
  verifier image identity and pristine workspace. Do not retry a successfully
  executed reward-zero verifier. `retries.maxAttempts` configures the number
  of additional agent-side whole-trial or verifier-only executions, and
  `retries.backoffSeconds` delays each additional execution.
- Persist the aggregate lifecycle log as `run.jsonl` inside the selected
  `all-N` run directory by default. This log retains admission order, phase
  transitions, retry history, timing, and run-wide progress. An explicit
  `--log-file` path overrides the default location. The completion line prints
  the resolved path. While the run is active, Wharf creates or refreshes a
  `run.jsonl` symbolic link in the launch directory, so `wharf progress` works
  from the same shell without locating `all-N`. Wharf never overwrites a
  regular file at that path; pointer failures are retained in the aggregate
  log.
- Delete terminal agent workspaces after the patch, stdout, stderr, trajectory,
  Copilot logs, verifier output, and final result have been persisted. This
  removes build outputs, package caches, and tool homes without changing the
  retained scoring evidence. Use `--retain-workspaces`, manifest
  `retainWorkspaces: true`, or `WHARF_RETAIN_WORKSPACES=1` for a diagnostic run
  that must preserve writable workspaces. Cleanup failures remain visible in
  the lifecycle log and `result.json`.
- Remove each task-derived agent image after its logical trial and each
  verifier image immediately after that verifier finishes or fails. Cleanup is
  based on explicitly registered image ownership; never delete base images,
  pulled ECR images, or unrelated tags by name pattern. Do not prune Docker
  build cache as part of run cleanup.
- Capture the complete patch from the pre-agent commit through the final
  workspace, including agent commits, later uncommitted edits, and new
  non-runtime files. Run the verifier whenever that patch is nonempty even if
  the agent exits nonzero, times out, or is stopped after an OOM; the verifier's
  reward remains authoritative while the agent failure evidence is retained.
- During the mountless Docker spike, reinstall the pinned CLI in each logical
  trial container so derived images can be removed immediately. Revisit
  cross-attempt derived-image caching only after the spike determines whether
  mountless execution changes the unexplained error rate.
- Keep different Copilot versions isolated; never silently reuse an image built
  for another requested version.
- Provide explicit image inventory, retention, pruning, and cleanup behavior
  so large task suites do not leave unbounded local image storage.
- Preserve enough image metadata to restart or inspect an agent run with
  Docker after the run has started.
- Add resource admission and host-profile validation for high-concurrency
  workloads, including the planned N=160 and N=256 profiles.
- Include storage-aware host profiles. The 64-vCPU profile uses a
  network-attached disk with higher latency and lower throughput than local
  storage; qualification must measure Docker layer, workspace, log, and
  verifier I/O under that constraint.
- Preserve the MVP's no-derived-agent-image startup path for this profile.
  Earlier reference-run image builds took more than ten minutes at full load and
  limited a 64-vCPU host to approximately N=80. Avoiding those builds is an
  important cold-start and concurrency advantage until a persistent,
  rate-limit-safe image cache is available.

## Non-goals for v1

- native Windows workers and native PowerShell execution
- Kubernetes deployment
- multi-node orchestration
- a hosted control plane
- a hosted dashboard
- licensing or task redistribution automation
- support for Linux distributions other than the documented Ubuntu LTS release
- byte-for-byte reproduction of another benchmark runner's internal implementation

## Supported platform

- Support Ubuntu 24.04 LTS on amd64 as the primary host platform.
- Support Ubuntu 24.04 LTS on WSL2 as a validated lower-throughput host.
- Defer native Windows workers and native PowerShell execution.
- Deliver Wharf as a versioned, signed `.deb` plus an official bootstrap script.
- Treat a host provisioned by the official bootstrap script as the supported unit.
- Use the same underlying installer for cloud-init, on-prem Ubuntu, and WSL bootstrap paths.
- Encapsulate all required Docker daemon, kernel, networking, storage, and limit changes in the bootstrap script.
- Make host provisioning idempotent and fail explicitly on incompatible existing configuration.
- Provide `check` and `smoke` commands that verify the complete supported host configuration.
- Allow deployment on public cloud, private cloud, on-premises hardware, or WSL.
- `check` must reject unsupported hosts unless the operator supplies an explicit unsupported-host override

## Language
- implement Wharf in .NET 10 LTS using idiomatic C# best practices
- use an SDK-style executable project with a thin `Program.cs` process entry point
- keep reusable implementation in responsibility-based namespaces under `src/wharf`
- define the Wharf version in exactly one easy-to-find source value
- every command, report, artifact, and package metadata source must read the version from that single value
- the default source value must support local development builds
- allow release builds to override the version through a documented MSBuild
  property or generated assembly metadata value
- `wharf version` and `wharf --version` must print the effective version, including a build-time override
- use CLI best practices including
  - use a maintained .NET command-line parser behind a Wharf-owned command boundary
  - --version - return the semver with no additional text or punctuation
  - --help -h - display the help (for that command)
  - --dry-run -d - dry run support
  - completion - support for dynamic tab completion and typeahead for bash and zsh
- structured logging
  - tab delimited by default for readability
  - json optional for programattic access
  - human-readable logs begin with `00:00:00 started=<absolute date-time>` and
    prefix later records with elapsed `HH:mm:ss`; durable JSON timestamps remain
    absolute
  - `wharf progress` reports trial and agent-infrastructure retry transitions
    as `retries=<count>` on the first summary line and active lifecycle
    occupancy as `building=<count>`,
    `agent=<count>`, `verifier=<count>`, and `cleanup=<count>` after the
    aggregate running count. Agent infrastructure retries remain agent work,
    whole-trial retry backoff remains building work, and post-verifier evidence
    cleanup is reported separately so the lifecycle counts sum to `running`.
    Terminal fields are printed first as `completed`, `pass-rate`, `pass@`,
    `failed`, `errors`, `timeouts`, `retries`, and `missing-grader`.
    `completed` includes every terminal trial independently from grader-result
    coverage. Missing grader results are classified by terminal outcome in
    verbose and JSON output. `failed` counts successfully graded reward-zero
    outcomes, `timeouts` counts bounded phase expirations, `errors` counts other
    infrastructure failures, and `pass-rate` is `pass@ / completed`; raw record
    counts remain available only in JSON output. Human output places terminal
    results, `queued`, active occupancy, and costs on separate lines. Queue
    wait summaries group measured preparation-gate waits by operation and K
    wave. Per-K lines are omitted for K=1 and
    remain hidden for a multi-K run until attempt 2 has actually started; once
    shown, they group results by the `attempt=K/total` identity rather than the
    configured total K. `wharf progress -v` appends every active trial as
    unlabelled duration, stage, and name columns, ordered by descending time in
    the current stage. The stage column is padded so task names align. Runs with
    structured stage events use the full task ID; older logs retain their
    historical shortened task names.
  - agent evidence retains Copilot's raw AI Credit count; progress converts each
    AI Credit to USD at $0.01 and displays average and total cost with two decimal
    places
  - the run-local `run.jsonl` contains additive durable stage events for
    `preparation`, `agent`, `verifier`, and `cleanup`; each event includes
    `eventType`, `runNumber`, `plannedRuns`, the full `taskId`,
    `logicalAttempt`, `trialRetry`, `stage`, and `transition`
  - stage completion events also include `result` and non-negative
    `durationMilliseconds`; failed completions retain `error` and `errorType`
    when available, and skipped or cancelled stages report that result
    explicitly
  - verifier duration includes verifier-image preparation and verifier execution
    but excludes verifier-image cleanup
  - query completed verifier timings from one run with:
    `jq -s '[.[] | select(.eventType == "stage" and .stage == "verifier" and .transition == "completed") | {taskId, logicalAttempt, trialRetry, result, durationMilliseconds}]' all-N/run.jsonl`
  - verifier command success means the grader executed; when retained
    `reward.json` reports `reward=0`, Wharf records a failed test rather than an
    infrastructure error
  - console verbosity
  - default output shows concise job progress, failures, final summaries, and one aggregate warning for each image pull that encountered one or more 429 responses
  - `--verbose` and `-v` add detailed lifecycle and retry summaries
  - `-vv` logs every 429 retry with its attempt number and selected delay
  - `-vvv` adds commands and subprocess diagnostics with secrets redacted
  - `-v` is reserved for verbosity, so version uses `--version` or `wharf version`

## Features
- when I stop a run, wharf should automatically stop and delete only the docker processes and ephemeral docker resources
- wharf must retain all files, directories, logs, patches, trajectories, verifier evidence, cost data, and run state
- retention defaults to retain everything
- deleting retained data requires a separate explicit prune command
- wharf must read existing legacy Pier 0.3.1 job directories
- wharf must emit the documented legacy Pier-compatible trial artifact subset
- Wharf-native metadata may be additive and does not need to be byte-for-byte compatible with the legacy format
- if the agent finishes, the patch should be saved in the run directory to make reuse easier
- if a test fails validation, wharf should first create a deterministic sanitized failure record
- wharf may ask a separately identified LLM summarizer to render the sanitized record into an explanation and a prompt for trying again
- the LLM rendering may not add facts that are absent from the deterministic record
- the feedback can NOT give away the tests that are run for validation, hidden test source, reference solutions, verifier paths, fixtures, or solution-revealing expected values
- preserve both the deterministic record and any LLM-rendered explanation
- wharf should look for infrastructure related issues that cause tests to fail and flag them for further review
- wharf should create a summary, similar to the summaries in ./data - include pass/fail, cost information, wall clock time for the agent portion, tokens (in, out, cache), steps
- wharf v1 needs to support Copilot CLI
  - need to be able to pin CLI versions for the test and have them in the ./job/* description
- wharf v1.1 adds Claude Code and Codex
- the run.sh capabilities should be in wharf
  - I should be able to run all, smoke, go, ts, python, rust, javascript, random N
    - I should be able to combine the languages - i.e. go python
- I should be able to turn on verbose logging with `--verbose`, `-v`, `-vv`, or `-vvv`
- most of the time, wharf will be running in the background via tmux - avoid repainting the output and provide a way to get the current status
- the reference runner states of starting environment, running agent, and verifying are good - add pulling image and building image to the list
- wharf should also compute the "enterprise score" for each run

## Initial scope

- Use the Harbor task format.
- Run the DeepSWE suite as the initial task provider.
- Support single tasks, full datasets, and custom task datasets.
- Add enterprise Java and .NET task suites after the DeepSWE path is stable.
- Keep task-provider boundaries general enough for private enterprise suites.
- Use Docker as the initial execution backend.
- Keep a worker interface that does not prevent future non-Docker backends.
- keep scheduler and worker boundaries explicit without implementing distributed coordination in v1
- do not implement licensing or task redistribution automation in v1
- record task provenance when it is already supplied by the task suite

## CLI contract

- the stable v1 command surface is
  - `wharf check`
  - `wharf smoke`
  - `wharf run`
  - `wharf resume`
  - `wharf status`
  - `wharf stop`
  - `wharf report`
  - `wharf completion`
  - `wharf version`
- the repair and replay phase adds
  - `wharf repair`
  - `wharf compare`
- reserve and document stable exit codes for success, invalid input, unsupported host, blocked run, partial run, and internal failure
- configuration precedence is command flag, environment variable, manifest, then built-in default
- unknown commands, flags, configuration keys, models, effort values, and adapter options must fail explicitly
- dry-run must validate configuration and show planned tasks and resources without creating run state, modifying Docker, or consuming model quota
- status must be readable while a run is active without attaching to tmux or mutating run state

## Task compatibility

- Read Harbor task metadata, instructions, environment definitions, artifacts, tests, and verifier settings.
- Load optional versioned `agent-duration-estimates.json` metadata from the task
  root. Each entry retains its language, median agent and verifier execution
  seconds from a named reference run, and a separate combined scheduling
  duration. These estimates do not alter task source checksums.
- Admit all attempt-1 work before attempt-2 work, continuing through K. Within
  each attempt wave, admit tasks by descending combined agent-plus-verifier
  scheduling duration with stable task and attempt IDs as tie-breakers. The
  `--shortest-first` run flag temporarily reverses only the duration ordering
  within each wave so early task results arrive sooner; longest-first remains
  the default for minimizing the final drain tail. The
  DeepSWE estimate profile spaces
  duration-ordered Rust tasks at positions 1, 17, 33, 49, and 65 so their
  resource-heavy builds are not front-loaded. Tasks without estimates follow
  estimated tasks in stable order.
- Respect task CPU, memory, storage, GPU, build-timeout, agent-timeout, and verifier-timeout declarations.
- Support separate agent and verifier environments.
- Capture the agent patch and apply it to a pristine verifier environment.
- Never expose hidden tests or reference solutions to the agent.
- Support task-level environment variables and declared artifacts.
- Support no-network tasks while allowing agent-specific outbound network allowlists.
- Preserve task and environment checksums in results.

## Run configuration

- Support a manifest file for reproducible job configuration.
- use a versioned manifest schema
- Support CLI overrides for common settings.
- Support `K`, the number of attempts per task.
- Default full DeepSWE runs to `K=4`.
- Allow lower `K` values without requiring a smaller task subset.
- Support `N`, the maximum number of concurrent trials.
- Support separate concurrency controls for preparation and trial execution.
- Use `P` for shared agent and verifier preparation capacity, including image
  preparation, root workspace copying, ownership handoff, pinned CLI
  installation, pristine verifier workspace creation, and patch application.
  Release the `P` permit before the agent or verifier command starts so `N`
  remains an independent logical-trial concurrency limit.
- Apply the `P` gate to the complete agent and verifier preparation pipeline,
  including any required base-image pull.
- do not expose a separate pull-concurrency flag in v1
- Default preparation concurrency from available CPU, with operator override.
- Support per-phase timeout multipliers and explicit timeout overrides.
- Support bounded trial retries with configurable included and excluded exception types.
- Support force-build, environment retention, resource-policy, mount, and artifact options.
- Validate all run settings before creating output directories or consuming model quota.
- Pin agent CLI versions with `--cli-version` or `WHARF_CLI_VERSION` and record
  the effective version for every trial. CLI version follows the same
  flag-over-environment-over-manifest-over-default precedence as other run
  settings. Exact versions drive version-specific image preparation without
  consulting the host-installed CLI; `latest` resolves the currently published
  `@github/copilot` version from the npm registry before image preparation.
- Record requested and effective model and effort settings.
- Accept `default` as an effort setting and omit the provider effort argument
  so the selected model uses its own default effort.
- Treat model-specific maximum settings as the common `frontier` effort class in reports.
- Keep provider-native effort values in raw results.
- record the effective merged configuration with every run
- keep secret values out of the effective configuration record
- environment variable names and manifest keys are part of the versioned configuration contract

## Task ordering and selection

- Randomize task start order with a recorded deterministic seed.
- Use the same task order for `K=1`, `K=2`, and higher `K` runs with the same seed.
- Interleave attempts so early termination does not over-sample a narrow task subset.
- Support include and exclude filters with glob matching.
- Support deterministic random subsets for experiments.
- Support language-balanced batching for quota-window providers.
- Discover tasks from the dataset rather than maintaining duplicate task lists.
- Detect completed attempts and avoid rerunning banked work.
- Treat empty patches, incomplete attempts, and quota failures as unfinished work rather than banked results.

## Scheduling and lifecycle

- Keep the scheduler work-conserving while respecting all phase limits.
- Support safe unattended overnight runs.
- Resume a job after process, host, provider, or Docker interruption.
- Preserve completed trials when resuming and schedule only missing or explicitly retryable work.
- Store enough state to distinguish logical trials from infrastructure retries.
- Support graceful drain, cancellation, and deadline handling.
- Forecast whether a suite can finish inside an overnight window from historical durations and current capacity.
- Allow a lower steady concurrency on on-prem and WSL hosts without changing result semantics.
- Make large cloud VMs an acceleration option, not a requirement.
- On success, failure, cancellation, or stop, clean up per-trial containers, networks, volumes, and disposable images reliably.
- Cleanup must delete only Docker processes and ephemeral Docker resources.
- Ctrl-C requests cancellation so active agent and verifier lifecycles can
  retain evidence before removing their own containers and transient images.
  Explicit orphan cleanup selects only Wharf-labeled containers; it never
  matches unrelated resources by name.
- Cleanup must retain every run directory and file.
- Preserve reusable base and pulled registry images across trials and jobs.
  During the mountless Docker spike, task-derived images are disposable and
  removed after their logical trial.
- expose a separate explicit prune command for operators who choose to delete retained data

## State model

- use immutable run, trial, logical attempt, and infrastructure retry identifiers
- record state changes as append-only versioned events
- produce materialized job and trial summaries from the event stream
- write summaries and other mutable state using atomic file replacement
- make recovery and resume idempotent
- allow concurrent status readers without locking or changing the run
- distinguish logical attempts from infrastructure retries in state, results, and reports
- preserve enough state to recover after the Wharf process, host, Docker daemon, or provider is interrupted
- version persisted event, configuration, result, and summary schemas
- atomically retain a secret-free run plan with the exact selected task IDs,
  checksums, and effective policy so filtered runs resume without changing
  timeout, retry, retention, model, effort, or CLI-version behavior
- validate terminal result identity before classifying a logical attempt as
  complete; malformed terminal evidence remains pending and is archived only
  when execution actually resumes
- storage implementation details are deliberately left to the implementer as long as the state contract is satisfied

## Docker and host behavior

- Validate Docker availability before reading credentials or starting paid work.
- Require a sufficiently large Docker default address pool for concurrent trial networks.
- Detect address-pool conflicts with LAN or VPN routes during provisioning.
- Require container `nofile` limits that prevent false benchmark failures.
- Require sufficient inotify instances for concurrent Node and test workloads.
- Verify that Docker can allocate a trial network before a run.
- Remove only unused stale trial networks during preflight.
- Detect insufficient disk, memory, file descriptors, PIDs, and network capacity before launch.
- Use each task Dockerfile's configured base image without local substitution.
- Inspect the local image store before pulling a missing configured base image.
- Fail before agent execution when a configured base image cannot be prepared.

## Image pulls and builds

- Inspect the local image store before contacting a registry.
- Pull each distinct base image at most once concurrently.
- Serialize duplicate pulls of the same image.
- Bound image preparation and registry pull pressure through the shared `P`
  preparation gate.
- Do not add a separate pull semaphore or public pull-concurrency setting in v1.
- Retry HTTP 429 and registry rate-limit responses with capped exponential backoff, decay, and jitter.
- Repeated 429 responses must increase the delay and reduce retry pressure.
- A successful pull or a quiet recovery period may restore retry pressure gradually without exceeding the `P` gate.
- Do not retry deterministic pull failures as rate limits.
- Record every 429 response in the durable structured log with the registry, image, attempt number, selected retry delay, and eventual outcome.
- Record the total 429 retry count and cumulative retry delay for every affected image.
- Include total 429 responses, affected images, exhausted retries, and cumulative retry delay in the job summary.
- Default console output emits one aggregate warning per affected image after the pull succeeds or exhausts retries.
- `-vv` and `-vvv` emit each 429 retry to the console as it occurs.
- Bound concurrent agent and verifier preparation with a semaphore.
- Treat preparation concurrency as independent from trial concurrency.
- Protect disk, CPU, and memory from build storms.
- Generate unique task-derived agent image names for each logical trial.
- Register task-derived agent and verifier images explicitly as owned by Wharf.
- Remove owned task-derived images after the logical trial without deleting
  configured base images, pulled registry images, or unrelated local images.
- Do not discover cleanup targets through broad repository/tag patterns.

## Agent support

- Keep an importable agent-adapter interface.
- Support built-in and custom CLI agents.
- the v1 adapter is Copilot CLI.
- v1.1 adds Claude Code and Codex adapters.
- Muse and no-op adapters may be migrated or retained as non-release-gating adapters.
- Let adapters declare installation steps, versions, prompt templates, flags, environment, and network allowlists.
- Run agents headlessly without approval or clarification stalls.
- Capture uncommitted agent work reliably into the graded patch.
- Isolate agent configuration, sessions, and logs per trial.
- Disable agent self-update when an exact CLI version is requested.
- Support model, effort, subagent, tool, and provider overrides without silently ignoring invalid values.
- Record actual parent and subagent models, not only the requested model.
- Support bring-your-own-model endpoints and provider credentials.
- Fail fast on definitively invalid credentials or unavailable model access.
- Keep transient host-network failures distinguishable from invalid credentials.

## Secrets and enterprise controls

- Never place secrets in process arguments, persisted job configuration, or normal logs.
- Support environment, file, stdin, and future secret-manager credential sources.
  Resolved run credentials are passed transiently through the Docker process
  environment and are omitted from the retained run plan.
- Redact secrets from diagnostic output and exported artifacts.
- Restrict agent egress to adapter-declared endpoints.
- Keep verifier execution isolated from model credentials and agent-only network access.
- Allow fully customer-owned result storage and optional zero-egress operation.
- Retain source snapshots, patches, trajectories, logs, test output, cost data, and run state by default.
- Retain everything for v1.
- Do not implement automatic file retention expiry in v1.
- Produce a software bill of materials and checksums for released packages and images.
- bootstrap operations that modify the host or Docker must use explicit sudo
- run Wharf services with least privilege
- run trial containers unprivileged by default
- isolate agent and verifier credentials, networks, mounts, and files
- never make hidden verifier tests available in an agent or repair workspace
- document the supported trust boundaries and threat model

## Progress and operations

- Show live job progress and per-trial lifecycle states.
- Provide quiet mode and repeatable `-v` verbosity through `-vvv`.
- Make status readable without mutating or attaching to the running job.
- Report pending, starting, agent, verifier, completed, errored, cancelled, and retry counts.
- Record estimated completion time and occupied concurrency.
- Write durable structured logs independent of the interactive display.
- version the structured event and JSON log schemas
- treat the tab-delimited output as human presentation rather than a machine API
- Support morning-ready completion and blocked-run notifications.
- v1 must always write completion and blocked-run status to durable files
- v1 should support an optional webhook notification
- email and hosted notification integrations may be added later
- Fail the whole run early for run-wide blockers such as invalid credentials or invalid host configuration.
- Do not require an operator to monitor ordinary trial retries.

## Results and artifacts

- Store job-level and trial-level configuration and result records.
- Record start and finish times for environment setup, agent setup, agent execution, and verification.
- Preserve the model patch for every measured trial.
- Preserve binary reward, new-test counts, existing-test counts, and partial scores.
- Preserve CTRF reports, raw verifier output, and failure reasons.
- Preserve agent trajectories in ATIF or a versioned equivalent schema.
- Record token usage, provider requests, premium requests, credits, and cost when available.
- Preserve exception type and terminal trial state.
- Keep retry attempts distinguishable without double-counting replaced results.
- Support compacting completed jobs while retaining everything needed for scoring, cost, repair replay, and audit.
- Never silently discard evidence required to classify a timeout, empty patch, quota failure, or bad environment.

## Auditing and trustworthy scoring

- Distinguish genuine failures from build failures, verifier timeouts, bad base environments, empty patches, quota failures, crashes, and missing reports.
- Treat a verifier timeout on a non-empty patch as a measured zero when that matches the scoring policy.
- Detect base-test failures that make a trial untrustworthy.
- Refuse success-shaped output when required evidence is missing.
- Report incomplete score and cost coverage explicitly.
- Preserve raw evidence so scoring policies can be recomputed later.
- Support trial-level PASS@1.
- Compute PASS@1 against the planned `K * task-count` attempt universe; missing
  scheduled attempts score zero rather than reducing the denominator.
- Support language and task breakdowns.
- Support cost per trial, task, and passing trial.
- Support agent-only and validator-inclusive runtime reporting.
- Export one portable JSONL record per trial.
- Keep reporting usable after raw job directories are deleted.
- Build a data-only manifest without requiring access to raw jobs.
- Use `wharf analytics export` for canonical run export and
  `wharf analytics manifest` for the data-only index.
- retain everything by default
- preserve source file hashes and schema versions in exports
- static Markdown reports and machine-readable JSON or JSONL exports are the v1 reporting surfaces
- hosted dashboards are deferred

## Requirements artifacts

- keep `docs/wharf.md` as the human-authored source of truth until the PRD is generated
- generate `docs/wharf-prd.md` for human readers
- generate `docs/wharf.jsonl` as the canonical LLM handoff format
- generate the human PRD and JSONL file from the same claim ledger
- never edit the generated PRD and JSONL independently
- use stable IDs for every goal, non-goal, constraint, decision, requirement, acceptance criterion, deliberate choice, and open question
- record exact source path and heading provenance on every generated claim
- assign must, should, or could priority to generated requirements
- validate every JSONL line, unique ID, reference, and source before publishing the artifact
- record the source file SHA-256 hashes in the JSONL metadata record

## Enterprise score

- Keep the Enterprise Score separate from the DeepSWE PASS@1 score.
- Give every task equal weight.
- Average attempts within each task before averaging tasks.
- Score missing measurements and existing-test regressions as zero.
- For regression-free trials, award 80 points for new-test coverage and 20 points for a full pass.
- Preserve coverage and pass components separately from the combined score.
- Treat the current 80/20 formula as an initial hypothesis to be calibrated with repair-effort data.

## Repair and replay

- Save immutable initial patches and sanitized failure summaries as reusable replay inputs.
- Generate deterministic structured failure summaries before an optional LLM summarizer.
- use an allowlisted, versioned failure-summary schema
- test the sanitizer against a denylist of hidden-test fields and solution-revealing data
- do not place raw hidden verifier output in the repair workspace
- an optional LLM rendering may only restate facts from the deterministic summary
- Include pass counts, failure categories, production-code locations, compiler errors, crashes, and timeouts when safe.
- Exclude hidden test source, reference solutions, verifier paths, and solution-revealing expected values.
- Start each repair round in a fresh agent session and container.
- Seed the repair workspace with the prior patch and original task instruction.
- Re-run the complete verifier after every repair round.
- Apply fixed round, time, token, and cost budgets.
- Preserve every repair prompt, summary-schema version, patch, verdict, cost, and duration.
- Allow the repair agent to retain, modify, revert, or replace the inherited implementation.
- Support self-repair and cross-model repair.
- Support frozen replay suites for comparing new CLI and model versions cheaply.
- Include fully passing `n/n` controls to measure do-no-harm behavior.
- Keep fresh-start and finish-this-implementation scores separate.

## Repair metrics

- Report Repair@1 and Repair@N.
- Report rounds, time, tokens, and cost to green.
- Report new-test improvement and regression removal by round.
- Report new regressions and passing-control damage.
- Report whether inherited work was retained, substantially rewritten, or discarded.
- Compare inherited repair with a matched fresh-start run under the same budget.
- Report handoff completion lift and cost/time advantage.
- Build starter-by-finisher matrices across models and CLI versions.
- Report self-repair strength, finisher strength, handoff quality, and pair asymmetry.
- Use repair-to-green data to calibrate future Enterprise Score weights.

## Reporting and comparison

- Generate a concise report automatically at job completion.
- Compare a run with a selected prior or currently qualified configuration.
- Highlight score, Enterprise Score, cost, runtime, language, and task deltas.
- Highlight tasks that changed outcome and common failure categories.
- Highlight requested-versus-effective model, effort, subagent, and CLI-version drift.
- Keep execution and reporting separate so reports can be regenerated without rerunning agents.
- Support machine-readable output for enterprise dashboards and data products.
- keep human report styling deliberately unspecified

## Delivery and support

- Provide a thin cloud-init wrapper around the shared Ubuntu bootstrap.
- Provide an Ubuntu WSL bootstrap that reaches `check` and `smoke` automatically.
- Recommend dedicated Wharf hosts when Docker daemon changes could affect other workloads.
- Document validated hardware and concurrency profiles.
- Include a conservative auto-calibration mode for concurrency.
- begin with host checks and operator-configured safety limits
- use observed measurements to recommend concurrency without silently increasing operator limits
- keep the exact recommendation algorithm deliberately unspecified
- Optimize for one complete evaluation per night rather than peak benchmark throughput.
- Support signed upgrades and rollback between Wharf releases.
- Keep Kubernetes, multi-node orchestration, and native Windows out of the initial release.

## Development process

- document the supported .NET SDK and toolchain in the repository
- provide one command that formats, analyzes, unit tests, and integration tests a fresh clone
- maintain local fixtures for Harbor task parsing, state recovery, artifact generation, and reporting
- use golden fixtures for versioned configuration, event, result, report, and JSONL schemas
- test interrupted runs at image pull, image build, agent setup, agent execution, verification, and report generation
- test secret redaction using synthetic credentials
- test unsupported-host and invalid-configuration failures before model quota can be consumed

## Acceptance criteria

- v1 installs through the official bootstrap on Ubuntu 24.04 LTS amd64
- v1 reaches a passing check and smoke run on Ubuntu 24.04 LTS and Ubuntu 24.04 WSL2
- the v1 Copilot CLI adapter can pin its CLI version and records the requested and effective version
- a DeepSWE task can run in separate agent and verifier environments without exposing hidden tests to the agent
- a completed agent patch is retained and applied to a pristine verifier environment
- stopping a run removes only Docker processes and ephemeral Docker resources and retains every file and directory
- resuming an interrupted run does not rerun completed logical attempts or double-count infrastructure retries
- status reports every lifecycle state without attaching to tmux or mutating the run
- the `P` gate limits complete agent and verifier preparation and is released
  before execution
- duplicate image pulls and identical derived-image builds are coalesced
- registry rate limits use bounded jittered backoff and deterministic failures are not retried as rate limits
- every 429 is present in durable structured logs and affected-image summaries include retry counts and cumulative delay
- default console output aggregates 429 retries per image while `-vv` and `-vvv` show each retry
- secrets do not appear in arguments, persisted configuration, normal logs, reports, or exported artifacts
- PASS@1 and Enterprise Score match golden fixtures
- cost, tokens, steps, agent wall time, trial wall time, and coverage completeness are reported when the provider supplies them
- the same retained job produces the same static report and machine-readable export when reporting is rerun
- generated JSONL parses one object per line, uses unique stable IDs, resolves every reference, and records source provenance
- in the repair and replay phase, deterministic repair summaries pass the leakage sanitizer tests
- the reference DeepSWE K=4 workload completes on each documented host profile within its published overnight window with no completed trials lost
