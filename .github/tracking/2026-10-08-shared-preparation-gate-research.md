# Shared preparation gate research

## Scope

Replace the pre-1.0 public `B` build/preparation concurrency contract with `P`
preparation concurrency, apply one shared gate to agent and verifier
preparation, release the permit before verifier commands execute, and retain
enough telemetry to evaluate cold-start contention.

## Requirements

- This is an intentional breaking change. Do not retain `B`, `b`, `--b`,
  `WHARF_B`, or qualification `--build-limit` aliases.
- `P` must continue to be independent from `N` and capped at `N`.
- One shared gate must cover:
  - agent image inspect, pull, and build;
  - agent workspace materialization, ownership, and Copilot installation;
  - agent infrastructure-retry workspace preparation;
  - verifier image construction;
  - pristine verifier workspace copying and patch application.
- The shared permit must be released before any verifier command or verifier
  container runs.
- Durable telemetry must make capacity, active occupancy, waiters, operation
  type, and acquisition wait duration observable for cold-run analysis.

## Current configuration surface

- The strict schema `1.0` manifest uses JSON property `b`
  (`src/wharf/Configuration/RunConfiguration.cs:5-9`).
- Resolved configuration and overrides expose `B`
  (`src/wharf/Configuration/RunConfiguration.cs:49-72`).
- Resolution reads short `B` and `WHARF_B`, defaults to 4, and caps B at N
  (`src/wharf/Configuration/RunConfiguration.cs:148-152`).
- Validation names `B` in errors
  (`src/wharf/Configuration/RunConfiguration.cs:242-249`).
- The CLI exposes `--b`, passes it into overrides, and reads both environment
  names (`src/wharf/Command/CommandTree.cs:174-181`,
  `src/wharf/Command/CommandTree.cs:225-249`).
- Dry-run JSON and text render `b`
  (`src/wharf/Configuration/RunConfigurationRenderer.cs:12-47`).
- The profile command uses `--build-limit`, reports `B`, and its model exposes
  `RecommendedBuildConcurrency`
  (`src/wharf/Command/CommandTree.cs:628-671`,
  `src/wharf/Qualification/HostProfiles.cs:14-21`).
- Smoke configuration and configuration/profile tests construct or assert B.
- Current normative documentation and the LLM requirements ledger still call
  the setting build concurrency. Historical tracking artifacts should remain
  unchanged because they describe earlier releases.

Because manifest loading rejects unmapped properties, changing `b` to `p`
automatically makes old manifests fail explicitly rather than silently using a
default (`src/wharf/Configuration/RunConfiguration.cs:77-82`).

## Current gate boundary

- `RunService` creates one `PreparationGate(configuration.B)`
  (`src/wharf/Run/RunService.cs:112-115`).
- Initial agent image preparation and workspace preparation are inside the
  gate (`src/wharf/Run/RunService.cs:205-259`).
- Agent infrastructure-retry workspace preparation reuses the same gate
  (`src/wharf/Run/RunService.cs:295-320`).
- `PreparationGate` is a `SemaphoreSlim` wrapper with no operation
  classification, occupancy, wait timing, or event sink
  (`src/wharf/Run/PreparationGate.cs:3-39`).
- Verifier image construction and the entire verifier service currently run
  outside the gate (`src/wharf/Run/RunService.cs:423-480`).

## Verifier split point

`PristineVerifierService.VerifyAsync` currently performs two different
resource phases in one method:

1. Preparation:
   - validate request and retained patch;
   - create the evidence directory;
   - copy a pristine workspace;
   - reject accidental agent-workspace reuse;
   - apply the retained patch.
2. Execution and evidence:
   - run verifier commands sequentially;
   - retain each command result;
   - read metrics;
   - atomically write the trial result;
   - classify timeout and external termination.

The preparation work is at
`src/wharf/Verifier/VerifierExecution.cs:25-47`; command execution begins at
line 49. This is the exact safe permit-release boundary. Gating the existing
`VerifyAsync` call would incorrectly hold P for long verifier commands and
could starve new agents.

## Telemetry design

Extend `PreparationGate.ExecuteAsync` with an operation type and an optional
event sink. Emit structured transitions:

- `waiting`: waiter count incremented before semaphore acquisition;
- `acquired`: waiter count decremented, active count incremented, and
  `waitMilliseconds` recorded;
- `released`: active count decremented after success, failure, or cancellation.

Each event should contain:

- `eventType=preparation-gate`;
- `operation` (`agent`, `agent-retry`, or `verifier`);
- `transition`;
- configured `capacity`;
- current `active`;
- current `waiting`;
- `waitMilliseconds` on acquisition.

The logger already supports durable structured fields through its private
write path and provides the pattern used for stage events
(`src/wharf/Logging/WharfLogger.cs:94-128`,
`src/wharf/Logging/WharfLogger.cs:164-196`). Add a typed public logger method
rather than encoding telemetry only in human text.

The event stream is sufficient to derive peak occupancy, queue depth, and wait
distributions without expanding the stable `wharf progress` display in this
change.

## Recommended approach

1. Rename the active configuration contract completely from B/build to
   P/preparation:
   - manifest `p`;
   - records `P`;
   - CLI `--p`;
   - environment `P` and `WHARF_P`;
   - dry-run output `p`;
   - profile `--preparation-limit` and
     `RecommendedPreparationConcurrency`.
2. Keep schema version `1.0`. This is a pre-1.0 Wharf breaking change, and the
   existing strict loader will reject `b` as unknown.
3. Extend `PreparationGate` with operation-aware structured telemetry while
   preserving release-on-failure/cancellation.
4. Split `PristineVerifierService` into:
   - `PrepareAsync`, returning a validated prepared-verifier value containing
     the request, patch/evidence identity, and pristine workspace;
   - `VerifyPreparedAsync`, running commands and retaining results.
   Keep `VerifyAsync` as an internal composition convenience only if existing
   direct tests or library callers need it.
5. In `RunService`, acquire one verifier preparation permit around verifier
   image creation plus `PrepareAsync`, then release it before calling
   `VerifyPreparedAsync`.
6. Preserve image cleanup in the existing verifier `finally` block so failures
   during either preparation or execution do not leak verifier images.
7. Update focused configuration, gate, verifier, run-service, profile, smoke,
   CLI, documentation, and strict-old-surface rejection tests.

## Risks

- Accidentally gating verifier execution would create head-of-line blocking
  from long-running verifiers.
- Splitting verifier preparation must not weaken evidence-before-error behavior
  for timeout and exits 137/143.
- Cancellation between preparation and execution must still allow existing
  verifier image cleanup.
- A non-atomic telemetry counter implementation could report impossible
  occupancy under concurrency. Counter mutation and event snapshots must be
  synchronized or use a single lock.
- Broad replacement of B in historical tracking artifacts would rewrite
  release history. Only active source, tests, normative docs, repo memory, and
  current requirements should change.

## Not taken

- Gate the entire verifier: rejected because slow verifier commands would hold
  scarce preparation capacity.
- Add a separate verifier semaphore: rejected because it would not bound the
  combined preparation pressure that motivates the change.
- Keep B aliases: explicitly rejected for this pre-1.0 breaking change.
- Tune the default or z01's P value now: defer until warm and cold telemetry
  exists.
- Add preparation occupancy to the interactive progress line now: durable
  structured events are enough for the planned cold-run analysis.
