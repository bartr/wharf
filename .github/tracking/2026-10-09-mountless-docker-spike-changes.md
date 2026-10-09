# Mountless Docker spike changes

## Implementation

- Replaced agent source/workspace/home bind mounts with one named managed
  container per execution.
- Agent preparation initializes `/workspace`, `/home/wharf`, and
  `/logs/agent` inside the container and installs the exact Copilot version as
  UID/GID `1000:1000`.
- Agent execution writes stdout, stderr, trajectory, Copilot logs, and the
  complete binary patch inside the container.
- Required evidence is copied into the trial directory with `docker cp` before
  container removal.
- `--retain-workspaces` copies `/workspace` out; normal runs retain only durable
  evidence.
- Replaced verifier evidence/artifact mounts with container-local `/logs`
  directories and bidirectional `docker cp`.
- Verifier evidence is copied before container removal on success, failure, or
  timeout.
- Locally built agent and verifier images are explicitly registered as owned.
  They are removed after their logical trial, with process-exit fallback
  cleanup.
- Removed broad image cleanup based on `__` in a repository/tag. Base images,
  pulled ECR images, and unrelated local tags are not cleanup candidates.
- Added an opt-in `--shortest-first` run flag for spike runs that prioritize
  early results. Longest-first remains the default.

## Validation so far

- Build: passed.
- Focused agent, verifier, and retry tests: 46/46 passed.
- Docker smoke tests: 5/5 passed.
- Full Docker-backed suite: 164/164 passed in named tmux session
  `wharf-mountless-full-tests`.
- Formatting verification and `git diff --check`: passed.
- Smoke produced a nonempty copied agent patch, agent logs, model artifact, and
  verifier reward.
- Smoke removed its task-derived agent and verifier images.
- No managed containers remained after smoke.
- `mars-base:latest`, pulled ECR images, and an unrelated pre-existing derived
  image remained present.
