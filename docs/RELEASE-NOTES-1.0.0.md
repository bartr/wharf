# Wharf 1.0.0

Wharf 1.0.0 is the v1 qualification release for the trustworthy evaluation
workflow.

## Highlights

- Durable task, attempt, retry, recovery, operations, audit, and report
  boundaries
- Deterministic local smoke fixture and report regeneration
- Versioned Debian packaging with checksums, SBOM metadata, and optional GPG
  signatures
- Explicit-sudo installation and rollback wrappers
- Conservative `on-prem-32` and `on-prem-88` host recommendations
- Repeatable release qualification and requirement-to-test coverage matrix

## Qualification

Run:

```bash
./scripts/qualify-release.sh 1.0.0
```

The automated source-level matrix passed. Overnight DeepSWE K=4 and
Ubuntu/WSL hardware qualification require customer-owned infrastructure and
retained evidence; they are documented as external gates in
[release-qualification.md](release-qualification.md), not represented by
fabricated benchmark results.

## Package

Build the package and checksums with:

```bash
./scripts/package-wharf.sh 1.0.0 dist
```

Provide `SIGNING_KEY` to produce and verify a detached GPG signature.
