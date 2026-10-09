# Mountless Docker spike research

## Scope decisions

- Apply the spike to both agent and verifier containers.
- Use image contents and `docker cp`; do not use host bind mounts or named
  volumes for either execution boundary.
- Preserve `--retain-workspaces` by copying the completed agent workspace out
  of its container.
- Remove task-derived agent and verifier images after each logical trial.
- Never remove base images or pulled ECR environment images.

## Current agent boundary

- `DockerAgentRunner.BuildAgentArguments` uses `docker run --rm` with three
  host bind mounts for the read-only task source, writable workspace, and
  agent home (`src/wharf/Agent/AgentExecution.cs:309`).
- `DockerAgentWorkspacePreparer` runs two additional mount-backed containers:
  one copies `/app` into the host workspace and one installs the requested
  Copilot package into that workspace (`src/wharf/Agent/AgentExecution.cs:528`).
- The agent runs as `1000:1000`; the initialization and cleanup containers run
  as root to repair ownership on host-mounted files
  (`src/wharf/Agent/AgentExecution.cs:574`,
  `src/wharf/Agent/AgentExecution.cs:614`).
- Patch capture currently runs host `git` commands against the mounted
  workspace after the container exits (`src/wharf/Agent/AgentExecution.cs:417`).
- Agent stdout, stderr, trajectory, patch, result, and copied Copilot debug
  logs are persisted by `AgentExecutionService`
  (`src/wharf/Agent/AgentExecution.cs:12`,
  `src/wharf/Agent/AgentExecution.cs:175`).

## Current verifier boundary

- The verifier image contains its own pristine `/app` and hidden `/tests`.
  It receives only `/logs/artifacts/model.patch` as input and writes grading
  evidence under `/logs/verifier`; the smoke fixture documents that contract
  (`smoke-tasks/smoke-go/tests/Dockerfile:7`,
  `smoke-tasks/smoke-go/tests/test.sh:6`).
- `DockerVerifierRunner` bind-mounts the host verifier evidence and artifacts
  directories, then uses `docker run --rm`
  (`src/wharf/Verifier/VerifierExecution.cs:344`).
- The verifier service reads `reward.json` and writes command stdout/stderr,
  retained patch, and `trial-result.json` after command completion
  (`src/wharf/Verifier/VerifierExecution.cs:55`).
- Verifier images are already unique to a trial/retry and removed in a
  `finally` block (`src/wharf/Run/RunService.cs:458`,
  `src/wharf/Run/RunService.cs:521`).

## Current image cleanup

- A derived agent build receives the requested task-specific image name, while
  tasks with `environment.image` return the pulled image reference unchanged
  (`src/wharf/Image/DockerAgentImagePreparer.cs:39`).
- `IAgentImagePreparer.RemoveImageAsync` exists but normal run orchestration
  does not call it (`src/wharf/Image/DockerAgentImagePreparer.cs:16`).
- Process-exit cleanup removes every local image whose repository/tag contains
  `__`, which is broader than the current run and is not ownership-safe
  (`src/wharf/Docker/DockerRunCleanup.cs:7`).

## Recommended spike design

1. Create a named, managed agent container from the prepared image with no
   mounts and an idle main process.
2. Start it, initialize `/workspace`, `/home/wharf`, and `/logs/agent` as root,
   then install the requested Copilot version with `docker exec --user
   1000:1000`.
3. Execute Copilot with `docker exec --user 1000:1000`, writing stdout and
   stderr inside `/logs/agent`.
4. Capture the complete binary patch inside the container, including untracked
   files, then copy `/logs/agent` into the run evidence directory with
   `docker cp`.
5. Copy `/workspace` to the run directory only when needed for current
   orchestration and retention semantics; normal cleanup removes it exactly as
   today.
6. Always remove the named agent container after evidence extraction, including
   timeout, OOM, cancellation, and failed-agent paths.
7. For each verifier command, create a named container without mounts, copy
   `model.patch` into `/logs/artifacts`, start/attach with the existing timeout
   behavior, copy `/logs/verifier` back, then remove the container.
8. Track whether an agent image was built from a local Dockerfile. Remove only
   those task-derived image names after the logical trial. Never infer cleanup
   eligibility from broad name scanning and never remove returned ECR/base
   references.

## Risks

- Container removal must happen only after logs, patch, and optional workspace
  extraction; otherwise failure evidence is irrecoverable.
- `docker cp` must treat a missing required patch/log as an explicit execution
  failure, not as empty successful evidence.
- Timeout and OOM handling currently controls the `docker run` container. With
  an idle container plus `docker exec`, stopping the named container must still
  terminate the active exec process and preserve the observed classification.
- Patch generation inside the container must retain the existing binary and
  untracked-file behavior and exclude Wharf runtime paths.
- Verifier evidence must be copied before image/container cleanup even when the
  command exits nonzero, times out, or is terminated.
- Removing an image after a logical trial must not target a pulled
  `environment.image`; cleanup needs explicit ownership metadata.

## Not taken

- No change to base-image pulls, ECR image caching, registry retry, image build
  contexts, N/P scheduling, memory limits, or verifier retry policy.
- No Docker named volumes.
- No broad image pruning or deletion by repository substring.
