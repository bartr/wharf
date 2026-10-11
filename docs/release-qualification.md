# Wharf 0.20.0 release qualification checkpoint

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

The implementation and benchmark gates are now evidenced:

* Real non-dry-run benchmark runs are represented by retained 113-task K=4
  exports under `data/`.
* The container-backed smoke path passed its Docker-backed validation suite.
* HydraFusion has two retained benchmark exports, `all-1` and `all-2`, with
  452 and 449 retained trial rows respectively.
* The benchmark runs were unattended; some ran across an overnight window.

The remaining MVP/1.0 action is to run or formally close the complete
acceptance matrix and record the release-owner decision. The unattended
benchmark evidence replaces “overnight without operator attention on each
trial” as the primary operational qualification criterion. Exact host,
configuration, timing, retry, recovery, and completed-trial-loss evidence
must remain attached to the authoritative qualification record.

## Evidence-backed gates

The reference DeepSWE K=4 workload evidence is present in the retained
analytics exports and was collected unattended, with overnight execution in
some cases. The authoritative qualification record must identify exact
hardware, configuration, timing, cost, retries, recovery, score completeness,
and completed-task preservation. The Ubuntu/WSL installation matrix remains
an acceptance-matrix item where target-host evidence is not already attached.

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
