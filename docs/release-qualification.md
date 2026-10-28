# Wharf release qualification

## 1.1.0 checkpoint, 2026-10-28

Release candidate: branch `1.1.0`, based on `8b099f9`, with version metadata
and a packaging correction. All long-running checks ran in the foreground
of tmux session `wharf-release-1-1-0` on z01.

| Gate | Evidence | Status |
| --- | --- | --- |
| Formatting | `dotnet format wharf.slnx --verify-no-changes` | PASS |
| Release build | `scripts/qualify-release.sh 1.1.0` | PASS |
| Full Release tests | 246 passed, zero failed/skipped; 26.5 s | PASS |
| Focused packaging regression | 2 passed; extracted package executable reports the requested version | PASS |
| Secret-pattern scan | `scripts/qualify-release.sh 1.1.0` | PASS |
| Debian package/checksum/SBOM | Retained under `jobs/release-1.1.0/package/` | PASS |
| Packaged executable | Extracted package runs and reports `1.1.0` | PASS |
| Docker-backed .NET smoke | HydraFusion, Copilot CLI 1.0.105, K=1/N=1/P=1; reward 1, F2P 1/1, P2P 1/1 | PASS |
| Debug wrapper | Debug build and `./wharf --version` report `1.1.0` | PASS |
| Read-only dry-run | New concurrency flags and defaults render without creating an output directory | PASS |
| Package signature | No signing key configured; `SIGNING.txt` explicitly labels the package unsigned | NOT CONFIGURED |

Smoke evidence is retained under
`jobs/release-1.1.0/smoke/copilot-hydrafusion-default-1.0.105/smoke-1/`.
Its patch is 330 bytes, agent-command duration 23.748 seconds, and measured
cost 7.19 credits ($0.0719). No trial or verifier retries were required.
Docker had no running containers after validation.

An initial .NET smoke command used the default Go task root and exited 2
before execution. More importantly, running the original extracted package
revealed exit 154 from a missing `wharf.dll`, despite metadata/checksum gates
passing. The package now includes the complete publish payload, with an
executable regression test and qualification gate. Failed package evidence
is retained separately under `jobs/release-1.1.0/package-incomplete/`.
See the [changes record](../.github/tracking/2026-10-28-release-1.1.0-changes.md)
and [review](../.github/tracking/2026-10-28-release-1.1.0-review.md).

Historical benchmark results retain their original provenance; this
checkpoint is not a new 113-task campaign or multi-host qualification.

## Historical 1.0.0 qualification

## Ownership

Release owner: **Wharf maintainers** (`WHARF_RELEASE_OWNER` may identify the
specific accountable operator when running the qualification script).

## Automated checkpoint matrix

| Gate | Evidence | Status |
| --- | --- | --- |
| Format | `dotnet format wharf.slnx --verify-no-changes` | PASS |
| Release build | `dotnet build wharf.slnx --configuration Release --no-restore` | PASS |
| Unit and boundary tests | `tests/wharf.Tests/` | PASS |
| Local smoke fixture | `wharf smoke` | PASS |
| Package/checksum/SBOM | `scripts/package-wharf.sh` | PASS |
| Upgrade/rollback wrappers | `scripts/wharf-bootstrap.sh`, `scripts/wharf-rollback.sh` | COVERED |
| Secret scan | `scripts/qualify-release.sh` | PASS |
| Report regeneration | `ReportRendererTests` | PASS |
| Host recommendation | `HostProfileTests` | PASS |

The implementation and benchmark gates are accepted for the 1.0.0 release:

* Real non-dry-run benchmark runs are represented by retained 113-task K=4
  exports under `data/`.
* The container-backed smoke path passed its Docker-backed validation suite.
* HydraFusion has two retained benchmark exports, `all-1` and `all-2`, with
  452 and 449 retained trial rows respectively.
* The benchmark runs were unattended; some ran across an overnight window.

The acceptance matrix is closed for the 1.0.0 release. Unattended execution,
including overnight execution where applicable, is the primary operational
qualification criterion. The retained summaries and job directories provide
the authoritative benchmark evidence, including host, configuration, timing,
retry, recovery, cost, and completed-trial accounting.

## Evidence-backed gates

The reference DeepSWE K=4 workload evidence is present in the retained
analytics exports and was collected unattended, with overnight execution in
some cases. The corresponding Wharf and DeepSWE reference job directories are
maintained with the benchmark evidence on the execution host. Exact hardware
and configuration provenance remain part of the retained run records.

## Requirement-to-test coverage

| Requirement area | Covering implementation/tests |
| --- | --- |
| Configuration and CLI | `RunConfigurationTests`, `WharfAppTests` |
| Harbor input and state | `HarborTaskTests`, `StateStoreTests` |
| Host and image preparation | `HostCheckTests`, `ImagePullCoordinatorTests`, `DerivedImageCoordinatorTests` |
| Agent and verifier isolation | `AgentExecutionTests`, `VerifierExecutionTests` |
| Scheduling, retry, recovery, operations | `SchedulerTests`, `PhaseExecutionTests`, `RecoveryPlannerTests`, `OperationsTests` |
| Audit and scoring | `AuditScorerTests` |
| Deterministic reporting | `ReportRendererTests` |
| Smoke and packaging | `SmokeServiceTests`, `PackagingScriptTests` |
| Scale recommendation | `HostProfileTests` |

Post-v1 repair/replay, hosted dashboards, alternate agent products, and
distributed orchestration remain outside the v1 scope.
