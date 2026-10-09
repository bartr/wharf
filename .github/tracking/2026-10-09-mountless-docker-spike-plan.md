# Mountless Docker spike plan

Research source:
`.github/tracking/2026-10-09-mountless-docker-spike-research.md`

## Fit check

This is a bounded spike on the existing `docker` branch. It changes only the
Docker agent/verifier execution and owned-image cleanup paths. Registry pulls,
build contexts, scheduling, retries, schemas, and benchmark scoring remain out
of scope.

## Tasks

1. [x] Add explicit mountless Docker container operations.
   - Targets:
     - `src/wharf/Docker/`
     - focused unit tests
   - Support create, start, exec, copy-to, copy-from, stop, and remove with
     argument-list process invocation.
   - Return stdout, stderr, and exit code without shell construction on the
     host.
   - Exit criterion: tests prove generated execution commands contain no
     `--mount`, `--volume`, or `-v`.

2. [x] Replace mount-backed agent preparation and execution.
   - Targets:
     - `src/wharf/Agent/AgentContracts.cs`
     - `src/wharf/Agent/AgentExecution.cs`
     - `tests/wharf.Tests/AgentExecutionTests.cs`
   - Create one named idle container per agent execution.
   - Initialize `/workspace`, `/home/wharf`, and `/logs/agent` inside it.
   - Install and execute Copilot as UID/GID `1000:1000`.
   - Write stdout/stderr and a complete binary patch inside the container.
   - Copy required evidence to the run directory before removing the
     container.
   - Copy the full workspace only when `--retain-workspaces` is effective.
   - Preserve timeout, cancellation, OOM, usage, retry, and empty-patch
     behavior.
   - Exit criterion: focused tests cover mountless arguments, evidence copy,
     workspace retention choice, and unconditional container removal.

3. [x] Replace mount-backed verifier execution.
   - Targets:
     - `src/wharf/Verifier/VerifierExecution.cs`
     - `tests/wharf.Tests/VerifierExecutionTests.cs`
   - Create and start a named verifier container without mounts.
   - Copy `model.patch` into `/logs/artifacts`.
   - Execute each verifier command under the existing timeout behavior.
   - Copy `/logs/verifier` and command logs to the run directory before
     container removal on success, nonzero exit, timeout, termination, or
     cancellation.
   - Exit criterion: focused tests prove there are no mount flags and that copy
     precedes removal.

4. [x] Make task-derived agent image cleanup explicit and trial-scoped.
   - Targets:
     - `src/wharf/Image/DockerAgentImagePreparer.cs`
     - `src/wharf/Run/RunService.cs`
     - `src/wharf/Docker/DockerRunCleanup.cs`
     - related tests
   - Distinguish a locally built task image from a pulled environment image.
   - Remove each locally built agent image after its logical trial evidence is
     complete, including failed/retried trials.
   - Preserve existing per-attempt verifier-image cleanup.
   - Remove broad image-name cleanup that could delete images not owned by the
     run.
   - Exit criterion: tests show derived images are removed and base/ECR image
     references are never cleanup targets.

5. [x] Update documentation and durable change evidence.
   - Targets:
     - `docs/wharf.md`
     - `.github/copilot-instructions.md`
     - `.github/tracking/2026-10-09-mountless-docker-spike-changes.md`
   - Document the spike’s mountless transfer contract and cleanup ownership.
   - Exit criterion: documentation no longer describes bind-mounted agent or
     verifier execution as current behavior on this branch.

6. [x] Validate and review.
   - Run focused agent, verifier, image, run-service, and cleanup tests.
   - Run formatting verification and `git diff --check`.
   - Run the full Docker-backed suite in a named tmux session.
   - Inspect Docker state after smoke completion to confirm task-derived images
     and managed containers are gone while `mars-base:latest` and pulled ECR
     images remain.
   - Write
     `.github/tracking/2026-10-09-mountless-docker-spike-review.md`.
   - Exit criterion: all checks pass, smoke evidence includes copied agent
     patch/logs and verifier evidence, and Docker cleanup is narrowly scoped.

## Parking lot

- Measure benchmark error-rate change only after the spike passes smoke and a
  separately framed controlled run.
- Decide whether this experiment becomes the permanent execution contract.
- Optimize `docker cp` volume and workspace retention only after correctness is
  demonstrated.
