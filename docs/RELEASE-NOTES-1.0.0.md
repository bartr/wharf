# Wharf 1.0.0

Wharf 1.0.0 is the first MVP release of the customer-controlled benchmark
execution platform.

## Highlights

* Deterministic Harbor and DeepSWE task selection with reproducible manifests.
* Isolated Copilot agent and verifier execution with durable evidence capture.
* Unattended scheduling, bounded retries, recovery, resume, status, and stop
  operations.
* Mountless Docker execution with explicit container and image ownership.
* Container-backed smoke validation through the same execution boundaries used
  by benchmark runs.
* HydraFusion provider support with two retained benchmark runs.
* Portable analytics exports, planned-denominator PASS@1, task accuracy,
  cost/usage evidence, and manifest validation.
* Versioned packaging, checksums, SBOM metadata, secret scanning, and
  upgrade/rollback coverage.

## Qualification

The 1.0.0 acceptance matrix is closed using retained summaries in `data/` and
the corresponding Wharf and DeepSWE reference job directories on the execution host.
Benchmark runs were unattended, with overnight execution where applicable.
The qualification record preserves run configuration, timing, retries,
recovery, cost, score coverage, and completed-trial accounting.

See [release-qualification.md](release-qualification.md) for the acceptance
matrix and [../RESULTS.md](../RESULTS.md) for benchmark results.

## Scope boundary

Post-v1 work includes repair and replay, hosted dashboards, alternate agent
products, distributed orchestration, and larger validated scale profiles.
