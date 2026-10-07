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
concurrency gate are introduced separately.

## Repository layout

- `src/wharf/` contains the executable implementation.
- `tests/wharf.Tests/` contains automated tests.
- `docs/` contains requirements, session logs, and retrospectives.
- `.github/tracking/` contains Research, Plan, Changes, and Review artifacts
  for implementation sessions.
