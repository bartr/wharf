# Mountless Docker spike review

## Requirement review

| Requirement | Result |
| --- | --- |
| Use the existing `docker` branch | Satisfied |
| No host disk mounts for agent containers | Satisfied |
| No host disk mounts for verifier containers | Satisfied |
| Copy agent logs into the run directory | Satisfied |
| Copy the complete agent patch into the run directory | Satisfied |
| Copy verifier artifacts/evidence through Docker | Satisfied |
| Preserve `--retain-workspaces` | Satisfied by conditional workspace copy |
| Remove task-derived images after each logical trial | Satisfied |
| Preserve base images | Satisfied |
| Preserve pulled ECR images | Satisfied |
| Avoid broad image pruning | Satisfied |

## Code review

- Agent containers use explicit create, start, root initialization, UID/GID
  `1000:1000` setup/execution, evidence capture, `docker cp`, and remove steps.
- Agent patch capture still includes committed changes, later uncommitted
  changes, binary diffs, and untracked non-runtime files.
- Timeout, OOM, and cancellation paths stop the active container, restart only
  the idle container when necessary to capture evidence, then remove it.
- Verifier containers copy artifacts in and verifier evidence out before
  removal. Stopped timeout containers remain copyable.
- Local image ownership is registered only after a successful task/verifier
  build. Pulled `environment.image` references are never registered.
- Normal image cleanup happens after retained result evidence is written.
  Process-exit cleanup operates only on the explicit ownership set.
- The previous broad cleanup scan for every image tag containing `__` is gone.
- A source search found no `--mount`, `--volume`, or `type=bind` arguments in
  the agent or verifier implementation.

No implementation defects were found in read-only review.

## Validation

| Check | Execution | Outcome |
| --- | --- | --- |
| Build | Completed | Passed with zero warnings/errors |
| Focused agent/verifier/retry tests | Completed | 46/46 passed |
| Docker smoke tests | Completed | 5/5 passed |
| Full Docker-backed suite | Completed in `wharf-mountless-full-tests` | 164/164 passed |
| Formatting verification | Completed | Passed |
| `git diff --check` | Completed | Passed |
| Agent/verifier mount search | Completed | No mount arguments found |
| Managed container inspection | Completed | None remained |
| New smoke image inspection | Completed | None remained |
| Base-image inspection | Completed | `mars-base:latest` remained |
| ECR-image inspection | Completed | 114 pulled ECR images remained |
| Unrelated image inspection | Completed | Pre-existing manual verifier image remained |

## Residual spike risks

- The Docker smoke exercises success and cleanup but does not force a real
  cgroup OOM or 30-minute timeout. Those paths preserve the existing typed
  control flow and were reviewed, but a controlled benchmark is the first
  production-scale validation.
- `docker cp` adds transfer overhead. The spike intentionally prioritizes
  isolation and diagnostic value over throughput.
- Cross-trial derived-image reuse is intentionally disabled on this branch, so
  package installation and image-build time may increase.

## Review outcome

Approved for a controlled spike run. Do not infer that unexplained benchmark
errors are fixed until a separately framed run compares retained failure types
and rates with the mount-backed baseline.
