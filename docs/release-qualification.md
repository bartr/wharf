# Wharf 1.0.0 release qualification

## Ownership

Release owner: **Wharf maintainers** (`WHARF_RELEASE_OWNER` may identify the
specific accountable operator when running the qualification script).

## Automated matrix

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

## Evidence-backed gates

The reference DeepSWE K=4 workload and Ubuntu/WSL installation matrix require
customer-owned hardware, Docker, credentials, and an overnight window. They
are not claimed as completed by this source-only qualification run. Their
retained evidence must identify exact hardware, configuration, deadline,
cost, retries, recovery, score completeness, and completed-task preservation
before a profile is marked qualified.

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
