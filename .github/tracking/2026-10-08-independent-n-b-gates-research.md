# Independent N and B Gates: Research

## Problem

Wharf must sustain at least 32 concurrent agent trials while independently
limiting filesystem- and Docker-intensive preparation. The failed N=32 run
created 32 workspaces totaling 27 GiB at once, generated kernel writeback
warnings, and coincided with host memory pressure. Setting `B=12` would reduce
that pressure today, but it would also reduce effective agent concurrency to
12.

## Current Behavior

- `RunService` computes `imageConcurrency = min(N, B)` and uses that value as
  the `MaxDegreeOfParallelism` for the entire per-task lifecycle
  (`src/wharf/Run/RunService.cs:85`, `107-113`).
- Each outer worker retains its slot through image preparation, workspace
  creation, agent execution, patch handling, verifier preparation/execution,
  and completion (`src/wharf/Run/RunService.cs:114-281`).
- The root-owned `/app` copy and ownership handoff occur inside
  `DockerAgentRunner.RunAsync`, immediately before the long-running agent
  container starts (`src/wharf/Agent/AgentExecution.cs:107-163`).
- The same agent startup command creates home/cache directories and installs
  the pinned Copilot CLI into every workspace before invoking Copilot
  (`src/wharf/Agent/AgentExecution.cs:193-201`). This is also local
  network/filesystem preparation and currently bursts at N.
- Because workspace preparation is hidden inside the agent runner, the outer
  coordinator cannot release a build/preparation slot after the copy.
- `PhaseExecutionService` already implements independent semaphore gates for
  Build, Agent, and Verifier phases (`src/wharf/Execution/PhaseExecutionService.cs:3-26`,
  `28-89`).
- `PhaseResourceLimits` already models distinct Build, Agent, and Verifier
  limits (`src/wharf/Execution/ExecutionContracts.cs:10-27`), and its tests
  prove independent phase concurrency
  (`tests/wharf.Tests/PhaseExecutionTests.cs:7-31`).

## Required Semantics

- `N` is the maximum number of active logical trials/agents.
- `B` is the maximum number of concurrent preparation pipelines.
- A preparation pipeline includes image preparation, the complete root
  workspace copy plus `chown`, and the per-workspace pinned Copilot CLI
  installation.
- A `B` permit is released before Copilot starts.
- An `N` permit remains held for the logical trial through agent and verifier
  completion so total admitted trial occupancy remains bounded.
- Waiting for `B` must not consume one of only `B` outer worker threads; N
  admitted trials may wait asynchronously for preparation.
- Cancellation must interrupt both gate waits and active Docker operations.
- Preparation failure must not start the agent and must release both permits.

## Recommended Design

### 1. Extract workspace preparation from the agent runner

Add an `IAgentWorkspacePreparer` boundary and a Docker implementation containing
the existing root initialization command followed by a UID/GID `1000:1000`
setup invocation that creates home/cache state and installs the pinned Copilot
CLI. `DockerAgentRunner` should run only the UID/GID `1000:1000` agent against
an already prepared workspace.

This makes the expensive copy an explicit Build-phase operation and preserves
the rule that Copilot never runs as root.

### 2. Make the run pipeline asynchronous

Replace the synchronous `Parallel.ForEach` orchestration with asynchronous trial
tasks. Bound total trial occupancy at `N`, either with an outer N gate or
`Parallel.ForEachAsync` configured with `N`.

Do not use `B` as the outer degree of parallelism.

### 3. Apply B around the complete preparation pipeline

For each admitted trial:

1. Acquire the Build gate (`B`).
2. Prepare/check/pull/build the image.
3. Create and initialize the workspace as root.
4. Change workspace ownership to `1000:1000`.
5. Install the pinned Copilot CLI as `1000:1000`.
6. Release the Build gate.
7. Start the agent under the N-bounded trial slot.
8. Run verification before releasing the trial slot.

Use the existing `PhaseExecutionService` Build gate rather than adding another
independent semaphore. This preserves the established phase timeout, retry, and
evidence model.

### 4. Keep verifier gating explicit

For the first change, set Agent and Verifier resource limits to N so behavior is
unchanged except for the independent B boundary. A later measured change can
introduce a separate verifier limit without changing N/B semantics.

## Expected Behavior

With `N=32 B=12`:

- Up to 32 logical trials may be active.
- At most 12 trials may simultaneously prepare images or copy workspaces.
- As soon as one copy completes, another waiting trial may prepare.
- All 32 agents can eventually run concurrently because completed preparations
  no longer retain B permits.

With `N=64 B=12`, preparation pressure remains the same while remote agent
occupancy may grow to 64. Memory use from active agent containers must still be
qualified independently.

## Validation Requirements

- A concurrency test must block preparation and agent operations independently
  and prove that maximum preparation concurrency equals B while maximum active
  agent concurrency reaches N.
- A sequencing test must prove the B permit is released before the agent waits
  or runs.
- Failure and cancellation tests must prove permits do not leak.
- Existing root-copy ownership tests remain applicable to the extracted
  workspace preparer.
- A live qualification run should start at `N=32 B=8` or `B=12`, record peak
  memory pressure, I/O pressure, writeback warnings, workspace growth, and
  verifier occupancy, then repeat at N=64 without raising B.

## Alternatives Rejected

- **Set B=N:** reproduces the simultaneous copy storm.
- **Set B below N without code changes:** lowers effective N because B controls
  the whole lifecycle.
- **Add a semaphore inside `DockerAgentRunner`:** limits copies but leaves image
  preparation outside the same complete B gate and duplicates existing phase
  gating.
- **Use only N and remove B:** cannot protect the host from preparation bursts.
- **Raise swap or disable systemd-oomd:** masks pressure instead of bounding its
  source.

## Recommendation

Implement the extraction and asynchronous phase orchestration before attempting
N=64. Qualify `N=32 B=12` first; if preparation pressure stays bounded, retain
B=12 while increasing N to 48 and 64.
