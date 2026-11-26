# User configuration changes

## Implemented

- Removed RunManifest, its loader and the public run option. Retained effective
  configuration serialization and analytics/Harbor manifests are unchanged.
- Added strict optional `~/.wharf/config.json` defaults for K/N/P, timeouts,
  retries, output format, workspace retention, scheduling order and container
  CPUs. Missing files do not create directories; inaccessible, malformed,
  unknown, duplicate and invalid settings fail explicitly.
- New runs, dry-run, prebuild and smoke load defaults; smoke fixes K=N=P=1
  and prebuild uses P without an N cap. Existing flags and environment settings
  override defaults; explicit false Boolean flags can override true defaults.
- Removed version pins and credentials from file input. CLI version is flag
  then selected-provider latest; obsolete version environment variables fail.
- Kept resume/batch continuation independent of current user defaults.
- Updated README, source requirements, corresponding PRD/ledger claims,
  continuous-improvement decision and contributor guidance. Historical release
  and session documents are explicitly superseded, not rewritten as new history.
  Pinned tasks, jobs, analytics exports and the pending quota checklist were untouched.

## Validation evidence

Commands ran foreground in named tmux `wharf-user-config-tests`:

```bash
dotnet test tests/wharf.Tests/wharf.Tests.csproj -c Release \
  --filter '(FullyQualifiedName~RunConfigurationTests|FullyQualifiedName~ClaudeCliTests|FullyQualifiedName~CodexCliTests|FullyQualifiedName~StateStoreTests|FullyQualifiedName~RetainedRunResumePlannerTests|FullyQualifiedName~BatchRunTests|FullyQualifiedName~SmokeServiceTests|FullyQualifiedName~WharfAppTests)&FullyQualifiedName!~RunCommandExecutesTaskDiscovery'
dotnet build src/wharf/wharf.csproj -c Release --no-restore
dotnet format --verify-no-changes --no-restore
```

- Final focused Release suite: **212 passed, 0 failed, 0 skipped**, exit 0.
  The paid discovery test was explicitly excluded, not counted as passing.
- Release build: exit 0.
- Full formatting: exit 2, pre-existing IDE0008 at
  `src/wharf/Agent/AgentExecution.cs:1007`. The same `var buffer` is present at
  HEAD; that file is unchanged. A subsequent `dotnet format wharf.slnx
  --verify-no-changes --no-restore --include ...` covering all 12 modified C#
  files passed, exit 0.
- Initial focused pass: 204/205, because the new removed-option test incorrectly
  expected semantic-validation exit 2. Actual parser rejection is exit 1.
  Corrected the test, preserving parser behavior; rerun passed 205/205 before
  adding process-level coverage.
- Process-level checks use isolated temporary homes: automatic config discovery,
  explicit false overrides, concurrency capping, provider/version flags, invalid
  config failure for run/dry-run/prebuild/smoke without output, and resume/batch
  preview despite invalid current config.
- Compiled run help has no removed option; analytics manifest help still works.
- Requirements JSONL: 390 valid rows, unique IDs. `git diff --check` passed.

No dependencies installed, paid trials launched, releases tagged or commits made.
Temporary test homes were removed. Validation establishes bounded configuration
behavior, not live-provider quality or a quantified productivity gain.

## Follow-up: initialize defaults on every invocation

The user changed the absence policy: every CLI invocation must create missing
`~/.wharf/` and `config.json`, including no arguments/help/invalid flags/dry-run.
Startup now initializes before parsing. It atomically renames a completed
default file without overwriting an existing or concurrently created config.
New Linux directories are user-only; existing directory/file permissions and
credentials are untouched. Defaults are derived with no flags/environment;
timeouts remain empty to preserve task-first timeout selection. Failures are
reported explicitly as invalid input. Existing config is not parsed at startup,
so malformed settings still cannot block help or retained-policy previews.

README, source requirements, corresponding PRD/ledger, continuous-improvement
decision, retrospective and contributor guidance document this sole startup-write
exception. The earlier absence/no-write statements above describe the original
implementation and are superseded by this requested follow-up.

Validation ran foreground in `wharf-config-init` with the same focused Release
test filter above, Release build and format verification for RunConfiguration,
WharfApp and their two modified test files:

- **226 passed, 0 failed, 0 skipped**, exit 0; paid discovery still excluded.
- Release build and four-file format verification: both exit 0.
- Process tests cover no arguments, help, quiet, version, dry-run, overridden
  concurrency, subcommand help and invalid flags/commands; generated config
  resolves identically to built-in defaults and existing config stays byte-identical.
- Concurrent initialization and existing-directory/credential/config preservation
  passed. Obstructing-file initialization fails explicitly without deleting it.
- Requirements JSONL remains valid with unique IDs; `git diff --check` passed.
  The actual user config directory/file are present.
- Initial follow-up validation failed to compile because new methods were
  inserted inside Load. Corrected method placement at class scope; all final
  checks above were rerun successfully. No failed execution is counted as a pass.

No paid work, dependency installs or commits. Isolated test homes are cleaned;
the user's initialized config is intentionally retained.
