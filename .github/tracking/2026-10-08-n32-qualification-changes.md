# N=32 qualification changes

## Qualification frame

- Host: z01, Ubuntu 24.04, Ryzen 9 8945HS, 8 cores/16 threads,
  60 GiB RAM, 8 GiB swap, local NVMe.
- Wharf run: 113 tasks, K=1, N=32, B=12, Copilot
  `gpt-5.6-luna` at max effort.
- Matching Pier job:
  `../jobs/copilot-gpt-5.6-luna-max-1.0.91/all-1`.
- Pier host: droplet with 40 vCPUs, 160 GiB RAM, and a locally attached
  500 GB NVMe disk.
- Pier configuration: K=4 and `n_concurrent_trials=115`.
- The Wharf job remained active while these changes were developed. Its loaded
  binary predates the fixes, so retained evidence must be rescored after the
  run rather than trusting its displayed pass count.

## Findings and changes

### Agent containment and preparation

- Disabled pnpm automatic package-manager version management in the agent
  environment to prevent recursive self-installation below the in-workspace
  home.
- Moved workspace materialization into a root initialization container, then
  assigned the copied tree to UID/GID `1000:1000` before agent execution.
- Raised future agent and verifier memory limits from 4 GiB to the dataset's
  uniform 8 GiB declaration, with no additional swap. Agent containers retain
  the 512-PID limit.
- Added Docker OOM-event monitoring. The first cgroup OOM kill stops the agent,
  retains evidence, and records `OutOfMemory`.

### Scoring and progress

- Verifier command exit zero means grading executed; it does not mean the task
  passed. `reward.json` is authoritative, and `reward=0` now fails the trial.
- Progress now reports all terminal trials in `completed`, failures as the
  `errors` subset, and `pass-rate = pass@ / completed`.
- Human logs use elapsed time, retain one absolute start timestamp, show
  building/agent/verifier occupancy, and render Copilot AI Credits as USD at
  one cent per credit.
- Cross-run cost comparisons use paired tasks with cost present on both sides
  and report cost per task, never cost per pass.

### Invocation and infrastructure failures

- Task instructions are passed as an attached `--prompt=<text>` argument so a
  prompt beginning with `-` cannot be parsed as another option.
- A Copilot `400 internal server error` is treated as retryable infrastructure
  failure. Wharf retries once in a fresh workspace, retains separate retry
  evidence, and includes both executions in reported cost.
- Independent task-manifest limits are now loaded and enforced for agent-image
  build, agent execution, verifier-image build, and verifier execution.
  Timed-out Docker processes and containers are terminated and typed timeout
  evidence is retained.

### Patch capture and failed-agent verification

- Patch capture now starts from the pre-agent commit and includes agent
  commits, later uncommitted edits, binary changes, and new non-runtime files.
  Wharf-managed home, cache, installation, and debug-log paths remain excluded.
- Any nonempty patch is sent to the verifier regardless of agent exit status,
  timeout, or OOM. Agent failure evidence remains visible, while verifier
  reward is authoritative for the trial score.
- `koota-query-predicates` empirically validated this policy. The active agent
  exhausted its 512-PID cgroup and was stopped after a complete patch was
  preserved. Manual verification produced:
  - F2P: 43/43
  - P2P: 172/172
  - reward: 1
- The preserved patch was 46,425 bytes across 13 files with SHA-256
  `79b5ad7ba895415f126a07bc07d0bc664cbf05536821b34cba761a6449ec0144`.
  The patch itself is not committed because benchmark task solutions and
  verifier-only material must not enter the Wharf repository.

## Commits produced during qualification

- `1650e47` - Guard agent container resources
- `021280b` - Improve run progress reporting
- `336eeb9` - Honor dataset container memory needs
- `3f29bff` - Harden trial failure handling
- `581b08f` - Enforce task phase timeouts

## Provisional comparison

At the clean 81-task comparison snapshot, after excluding every task with
confirmed cgroup OOM evidence:

- Wharf K=1: 60/81, 74.07%
- Pier earliest matching attempt: 59/81, 72.84%
- Pier all attempts: 223/324, 68.83%
- Pier task pass@4: 75/81, 92.59%

These are provisional snapshots, not final run results. Koota's later manual
reward must be included in the corrected final accounting.

## Deferred work

- Finish the active K=1 run and regenerate terminal status from retained agent
  evidence and authoritative verifier rewards.
- Rerun the same K=1/N=32/B=12 profile with the 8 GiB limits and the corrected
  binary.
- Consume per-task CPU, memory, storage, and PID declarations rather than
  retaining hard-coded runtime ceilings.
- Compare wall clock only from equivalent inner execution boundaries and
  comparable host/concurrency profiles.
