The Wharf 0.16.0 release has the preceding architecture but does not include
this release boundary. Implement the 0.16.0 to 0.17.0 change for Implement the local smoke workflow.

Use the historical public contract in the target release as the compatibility
boundary. Add the production types, validation, and behavior for the
Implement the local smoke workflow. Preserve existing behavior, do not modify existing test files, and
retain explicit errors rather than silent fallbacks.

The implementation should match the target release's public API and tests for
this boundary. Keep unrelated workflow artifacts out of the change.

Preserve the historical public contract in the `Wharf.Smoke` namespace.
Expose `SmokeService`, `SmokeResult`, and `SmokeException`, and preserve the
existing `Wharf.Command.CommandTree`,
`Wharf.Configuration.RunConfiguration`, and
`Wharf.Configuration.WharfConfiguration` types.

Use these historical public shapes:

- `SmokeResult(HostCheckReport Host, string Status, string ArtifactDirectory,
  string ReportPath, string TrialPath, int ExitCode)`; and
- `SmokeService.Run(string outputDirectory, HostCheckReport host)`.

Reject an empty output directory with `SmokeException`. A passing host check
returns status `"completed"` with exit code zero, distinct agent and verifier
workspace paths, a retained trial file, a report file, and `report.jsonl`. A
blocking host check returns status `"qualification-blocked"`, preserves the
host report's exit code, and writes `host-check.json` plus the report.
Write `report.jsonl` and `host-check.json` directly under the supplied
`outputDirectory`, not under an additional smoke subdirectory. The paths
returned by `SmokeResult` must identify files that exist when `Run` returns.

IMPORTANT: Do not commit changes. The evaluator captures the working-tree
patch after implementation.
