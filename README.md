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
