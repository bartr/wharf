# Shared preparation gate plan

Research source:
`.github/tracking/2026-10-08-shared-preparation-gate-research.md`

## Fit check

Proceed. The work fits one focused session if interactive progress rendering,
default tuning, and the cold benchmark remain out of scope. The releaseable
unit is the breaking public rename, shared preparation boundary, durable
telemetry, regression coverage, and normative documentation.

## Tasks

1. [x] Replace active B/build configuration with P/preparation.
   - Targets:
     - `src/wharf/Configuration/RunConfiguration.cs`
     - `src/wharf/Configuration/RunConfigurationRenderer.cs`
     - `src/wharf/Command/CommandTree.cs`
     - `src/wharf/Qualification/HostProfiles.cs`
     - `src/wharf/Smoke/SmokeService.cs`
     - configuration, CLI, profile, and smoke tests
   - Change manifest `b` to `p`, record members `B` to `P`, CLI `--b` to
     `--p`, environment `B`/`WHARF_B` to `P`/`WHARF_P`, and profile
     `--build-limit` to `--preparation-limit`.
   - Rename profile output and model fields from build to preparation.
   - Keep default 4 and cap P at N.
   - Add negative tests proving old manifest and CLI surfaces fail.
   - Exit criterion: no active source or nonhistorical test accepts or renders
     B.

2. [x] Add structured preparation-gate telemetry.
   - Targets:
     - `src/wharf/Run/PreparationGate.cs`
     - `src/wharf/Logging/WharfLogger.cs`
     - `tests/wharf.Tests/PreparationGateTests.cs`
     - `tests/wharf.Tests/WharfLoggerTests.cs`
   - Add operation-aware `waiting`, `acquired`, and `released` snapshots with
     synchronized active/waiting counters, capacity, and acquisition wait
     duration.
   - Emit durable `eventType=preparation-gate` JSONL records.
   - Prove permit release and valid counter bounds under success, failure, and
     cancellation.
   - Exit criterion: retained events can reconstruct occupancy, queue depth,
     operation type, and wait duration.

3. [x] Split verifier preparation from command execution.
   - Targets:
     - `src/wharf/Verifier/VerifierContracts.cs`
     - `src/wharf/Verifier/VerifierExecution.cs`
     - `tests/wharf.Tests/VerifierExecutionTests.cs`
   - Add a typed prepared-verifier value.
   - Move validation, pristine workspace creation, workspace separation, and
     patch application into `PrepareAsync`.
   - Move sequential command execution and evidence/error classification into
     `VerifyPreparedAsync`.
   - Preserve `VerifyAsync` as composition for existing direct callers.
   - Exit criterion: tests prove preparation performs no verifier command and
     execution preserves evidence-before-timeout/termination behavior.

4. [x] Share P across agent and verifier preparation.
   - Targets:
     - `src/wharf/Run/RunService.cs`
     - focused run-service tests
   - Construct `PreparationGate(configuration.P)` with logger telemetry.
   - Classify initial agent, agent retry, and verifier operations.
   - Acquire one verifier permit around verifier image construction and
     `PrepareAsync`.
   - Call `VerifyPreparedAsync` only after the permit is released.
   - Preserve verifier image cleanup for preparation and execution failures.
   - Exit criterion: a blocked verifier command does not consume P while
     concurrent preparation never exceeds P.

5. [x] Update version and normative documentation.
   - Targets:
     - `src/wharf/wharf.csproj`
     - `docs/wharf.md`
     - `docs/wharf-prd.md`
     - `docs/wharf.jsonl`
     - active session/repo memory
   - Set the development version to `0.23.0`.
   - Describe P as shared preparation capacity and verifier execution as
     outside the gate.
   - Do not rewrite historical tracking artifacts.
   - Exit criterion: current documentation and requirements use P
     consistently and describe the exact gate boundary.

6. [x] Validate and review.
   - Run focused configuration, gate, verifier, run-service, logger, profile,
     smoke, and CLI tests.
   - Run formatting verification, build, and the full unit suite.
   - Search active source/tests/normative docs for stale B surfaces.
   - Write changes and review artifacts with truthful results.
   - Exit criterion: all checks pass and review finds no verifier execution
     path holding P.

## Parking lot

- Add live preparation occupancy to `wharf progress`.
- Tune the default P and z01 run value from warm/cold telemetry.
- Decide whether image-pull pressure should eventually influence dynamic P
  admission rather than only retry backoff.
