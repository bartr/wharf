# User configuration research

## Outcome and evidence

Replace arbitrary per-run `--manifest` input with optional operator defaults at
`~/.wharf/config.json`, as approved in the conversation. Preserve exact retained
run policy and secret-free dry-run behavior.

- `src/wharf/Configuration/RunConfiguration.cs:7` defines the old run input;
  `:161` resolves layered configuration; `:307` validates effective settings.
- `src/wharf/Command/CommandTree.cs:216` exposes the old option; `:314` rejects
  immutable changes during batch continuation. Resume restores retained policy.
- `src/wharf/Run/RunService.cs` and `Run/BaselineRunService.cs` consume phase
  timeouts and retry policy. Mounts, artifacts, retention days and resource policy
  have no runtime consumers beyond configuration validation/rendering; preserve
  their retained fields without advertising new operational controls.
- `src/wharf/Configuration/DryRunService.cs:5` resolves without runtime resources.
- `tests/wharf.Tests/WharfAppTests.cs:158` contains a paid live discovery test;
  offline validation must exclude it.

## Recommendation and boundaries

Use strict optional JSON user configuration for K/N/P, phase timeouts, retries,
output format, workspace retention, shortest-first scheduling and container CPUs.
Flags precede supported environment variables, then user defaults, then built-in
defaults. Keep provider/model/effort, output location and baseline modes outside
the file; CLI version remains flag then provider latest. Keep credentials separate.
Missing configuration means defaults; malformed, inaccessible or unsupported
configuration fails before creating output. Do not automatically create the file.

Keep persisted effective configuration and analytics/Harbor manifests unchanged.
Update current documentation; explicitly mark historical run-input contracts
superseded rather than rewriting benchmark payloads or retained historical evidence.
No paid evaluation or measured performance/cost benefit is claimed.

## Startup initialization follow-up

The user subsequently requires every CLI invocation, including no arguments,
help/version, invalid flags, dry-run and resume, to create missing user config.
`src/wharf/WharfApp.cs:12` is the shared boundary before parsing;
`src/wharf/State/AtomicFile.cs:5` only replaces files, so it cannot safely create
user-owned defaults without overwriting a concurrent creator.
Initialize once at that boundary with an atomic, no-overwrite rename. Serialize
resolved built-in operational defaults using an empty environment; leave phase
timeouts unset so task timeout precedence does not change. Do not validate or
overwrite an existing file during startup: help and retained resume must not
become dependent on current config validity. Initialization failures must exit
explicitly. This intentionally adds user-config initialization to otherwise
read-only commands; run evidence, Docker and network behavior stay unchanged.
