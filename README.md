# Wharf

Wharf is an enterprise-focused .NET CLI for running trustworthy software
evaluations on customer-owned infrastructure. The DeepSWE task corpus and
existing benchmark tooling in this repository are its reference workload.

## Development

Wharf requires the .NET 10.0.401 SDK pinned by `global.json`. The executable
project is under `src/wharf`, with tests under `tests/wharf.Tests`.

Run the complete local validation workflow from a fresh clone with:

```bash
./scripts/validate-wharf.sh
```

The command formats and analyzes Wharf C# sources, runs unit and integration
tests, and builds the executable.

Release builds can override the source version with:

```bash
dotnet build -p:WharfVersion=<semver>
```

## Image preparation

The `Wharf.Image` library boundary inspects the local Docker image store before
pulling, coalesces duplicate image requests, and retries only explicit registry
rate limits with bounded exponential backoff and jitter. Image preparation can
write versioned evidence to `image-pulls.jsonl` and
`image-pull-summary.json`. Derived image builds and the complete preparation
concurrency gate are coordinated by `DerivedImageCoordinator`, which applies
the configured `B` limit across base preparation, context preparation, and
derived builds. Deterministic cache keys allow normal builds to be reused and
coalesced; force builds bypass the cache.

## Copilot adapter

The `Wharf.Copilot` boundary validates exact requested/effective CLI versions,
records native and normalized frontier effort, restricts endpoints to an
explicit HTTPS allowlist, and supports environment, file, and stdin
credential sources. Credential values are never included in invocation
arguments, environment configuration, or redacted command output.

## Agent execution

The `Wharf.Agent` boundary validates separate source and workspace mounts,
keeps hidden verifier material outside the agent, and provides an unprivileged
Docker runner with explicit network and mounts. Agent stdout, stderr,
trajectory, patch, timing, usage, and result evidence are retained. Empty
patches are recorded as unfinished rather than successful.

## Pristine verification

The `Wharf.Verifier` boundary copies task source into a fresh workspace,
applies a retained patch, and runs verifier commands sequentially with
explicit timeouts. Raw command output, the applied patch, and an atomic
versioned trial result are retained. The verifier never reuses the agent
workspace.

## Scheduling

The `Wharf.Scheduler` boundary admits attempts deterministically under
concurrency and trial limits, banks successful logical attempts, skips
duplicates, exposes lifecycle counts, and records cancellation transitions.

## Phase execution

The `Wharf.Execution` boundary gates build, agent, and verifier phases
independently, applies timeout multipliers and overrides, and retries only
classified infrastructure failures within a bound. Infrastructure retry
evidence is persisted separately from logical attempts.

## Recovery

The `Wharf.Recovery` boundary replays retained events, resumes only unfinished
or explicitly retryable logical attempts in stable order, preserves retry
identity, and reports orphaned live resources for explicit reconciliation.

## Operations

The `Wharf.Operations` boundary provides non-mutating status snapshots,
bounded graceful stop, targeted ephemeral cleanup, and durable completion or
blocked notifications. Optional webhook delivery is HTTPS-only.

## Repository layout

- `src/wharf/` contains the executable implementation.
- `tests/wharf.Tests/` contains automated tests.
- `docs/` contains requirements, session logs, and retrospectives.
- `.github/tracking/` contains Research, Plan, Changes, and Review artifacts
  for implementation sessions.
