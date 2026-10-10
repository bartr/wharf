# Scoring core changes

- Corrected PASS@1 to count passing logical attempts over the planned
  `K * expected-task-count` universe.
- Added separate task pass-at-least-once counts/rate and explicit task-equal
  Enterprise coverage and pass components.
- Updated report schema to 1.1 and rendered the new metrics unambiguously.
- Added canonical analytics contracts, native run evidence reading, deterministic
  one-row-per-logical-trial JSONL export, AI Credit conversion, retry-aware agent
  timing/usage, and retained task/test/patch/error evidence.
- Added a data-only manifest builder over portable `*.jsonl` exports.
- Added `wharf analytics export` and `wharf analytics manifest` command
  integration and operator documentation.
- Kept an explicitly unknown terminal cost null while retaining legacy
  agent-evidence fallback when the terminal property is absent.
- Added focused scoring, reporting, export, retry-coalescing, ordering, and manifest
  tests.

Validation:

- `dotnet test tests/wharf.Tests/wharf.Tests.csproj --filter
  'FullyQualifiedName~AuditScorerTests|FullyQualifiedName~ReportRendererTests|
  FullyQualifiedName~AnalyticsExporterTests|FullyQualifiedName~DataManifestBuilderTests'
  --no-restore --nologo`: 10 passed.
- `dotnet format wharf.slnx whitespace --no-restore --verify-no-changes --include
  <changed files> --verbosity quiet`: passed.
- `git diff --check`: passed.
- Native All-1 smoke export: 452 trials, 113 tasks, 447 costed trials, and
  $539.70 total cost, matching the canonical retained export.
- `./scripts/validate-wharf.sh`: 196 passed, 0 failed, 0 skipped.
