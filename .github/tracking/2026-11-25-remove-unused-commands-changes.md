# Remove unused commands: changes

- Removed status/stop placeholders and their factory, report/profile handlers
  and registrations from CommandTree.
- Deleted standalone report renderer/contracts and host-profile/qualification
  implementation, plus their exclusive tests. Removed status/stop services,
  interfaces/snapshot and tests from Operations; retained notification code/test.
- Added four process regression cases: unknown-command exit 1, absence from
  help, preserved every-invocation config initialization, and no job/run output.
- Updated README, contributor guidance, current source requirements and PRD.
  Updated 22 matching ledger claims with stable IDs and exact current source
  line provenance; refreshed the source hash. Historical session/release
  contracts are marked superseded, not rewritten.
- Preserved progress, resume, Ctrl+C cancellation, resource ownership, audit,
  smoke evidence, analytics implementation and pinned tasks/jobs/exports.

## Validation

Foreground tmux session `wharf-remove-commands`:

```bash
dotnet test tests/wharf.Tests/wharf.Tests.csproj -c Release \
  --filter '(FullyQualifiedName~WharfAppTests|FullyQualifiedName~OperationsTests|FullyQualifiedName~ProgressServiceTests|FullyQualifiedName~RetainedRunResumePlannerTests|FullyQualifiedName~BatchRunTests|FullyQualifiedName~AnalyticsExporterTests|FullyQualifiedName~DataManifestBuilderTests|FullyQualifiedName~SmokeServiceTests|FullyQualifiedName~ResourceCleanupTests)&FullyQualifiedName!~RunCommandExecutesTaskDiscovery'
dotnet build src/wharf/wharf.csproj -c Release --no-restore
```

- **129 tests passed, 0 failed/skipped**; Release build passed, both exit 0.
  Paid discovery was explicitly excluded.
- Format verification for all five changed C# files passed, exit 0. The
  unrelated AgentExecution IDE0008 is outside these modified files.
- Current source/tests/specs contain no removed command/type references.
  Whitespace and unchanged benchmark-directory checks passed.
- Ledger parses 390 unique rows; all ID references resolve and updated claims
  match source requirements. An additional metadata-count check failed on the
  pre-existing OQ-002 row: it has no record_type, status resolved, while metadata
  still counts three open questions. This unrelated baseline issue was retained,
  not silently counted as a pass or fixed during command removal.
- Initial launch failed at zsh history expansion of a double-quoted `!` filter,
  before any tests executed. Single-quoting the filter resolved it.
- A trailing blank line was removed after the first format check; no behavior
  changed. Final whitespace and modified-file format verification cover it.

[Analytics inspection](2026-11-25-analytics-inspection-research.md) records
four reproduced validation gaps. Analytics behavior and benchmark data were
not changed. No paid trials, dependency installations, commits or releases.
