# Remove container CPU controls: changes

## Implemented

- Removed `--container-cpus`, user/effective/override properties, rendering,
  DockerCpuSetAllocator and its probe, trial CPU-policy resolution, CpuSet request/
  result fields and affinity arguments in agent/verifier/baseline containers.
- Removed CPU-only allocator/policy tests. Adjacent lifecycle, memory/PID,
  ownership, evidence, pristine-workspace and timeout/retry tests remain.
- Obsolete `WHARF_CONTAINER_CPUS` fails explicitly with unset guidance, including
  run/batch, prebuild, smoke and resume. Strict user config rejects `containerCpus`.
  Startup still preserves existing user config, including invalid files.
- Legacy retained JSON CPU fields remain readable through the existing general
  deserializer. No retained run-plan or trial evidence is rewritten; resumed
  execution intentionally no longer applies the retired restrictions.
- Updated README, current specs/ledger, continuous-improvement removal decision,
  retrospective and contributor guidance. New monitor configuration snapshots
  no longer include the retired setting. Passive inspection of Docker's actual
  cpuset remains diagnostic telemetry, not allocation or configuration support.
- Pinned tasks, raw jobs, exports, scoring rules, N/P, container memory and
  agent/baseline PID limits are unchanged.

## Validation

Tests/build/format ran foreground in named tmux `wharf-remove-cpu`:

```bash
dotnet test tests/wharf.Tests/wharf.Tests.csproj -c Release \
  --filter '(FullyQualifiedName~RunConfigurationTests|FullyQualifiedName~WharfAppTests|FullyQualifiedName~AgentExecutionTests|FullyQualifiedName~VerifierExecutionTests|FullyQualifiedName~BaselineRunTests|FullyQualifiedName~RunServiceTests|FullyQualifiedName~RetainedRunResumePlannerTests|FullyQualifiedName~BatchRunTests|FullyQualifiedName~SmokeServiceTests|FullyQualifiedName~StateStoreTests|FullyQualifiedName~AnalyticsExporterTests)&FullyQualifiedName!~RunCommandExecutesTaskDiscovery'
dotnet build src/wharf/wharf.csproj -c Release --no-restore
```

- Initial combined suite: 220/220 passed. Final suite after adding process-level
  retired-key coverage: **221 passed, 0 failed, 0 skipped**, exit 0.
  Paid discovery is explicitly excluded, not counted as passing.
- Release build passed, exit 0; final test run also recompiled Release.
- Removed-flag parsing fails with exit 1 before run output. Process-level
  retired config-key validation fails with exit 2 and preserves the file.
- Compiled run, prebuild, smoke and resume reject the old environment variable
  with exit 2. Compiled run help has no CPU option.
- Legacy CPU run-plan fields load without affecting effective settings or
  changing the original bytes. Agent/verifier/baseline argument tests assert
  no `--cpuset-cpus` or `--cpus`, while retaining memory/ownership/PID checks.
- Modified-file format verification found only pre-existing IDE0008 at
  `src/wharf/Agent/AgentExecution.cs:1003` (old line :1007). HEAD contains the
  same `var buffer`; it was not changed. Rechecking all 18 modified C# files with
  `--exclude-diagnostics IDE0008` passed, exit 0. No unqualified format pass is claimed.
- Monitoring script AST parsing, 390-row requirements JSONL with unique IDs,
  `git diff --check` and unchanged benchmark-directory scope passed.

No tests failed, paid work launched, dependencies installed, raw evidence
deleted or commits created. One patch context mismatch was corrected before
validation; no changes were applied by that failed patch.
