The Wharf 0.15.0 release has the preceding architecture but does not include
this release boundary. Implement the 0.15.0 to 0.16.0 change for Implement deterministic report rendering.

Use the historical public contract in the target release as the compatibility
boundary. Add the production types, validation, and behavior for the
Implement deterministic report rendering. Preserve existing behavior, do not modify existing test files, and
retain explicit errors rather than silent fallbacks.

The implementation should match the target release's public API and tests for
this boundary. Keep unrelated workflow artifacts out of the change.

Preserve the historical public contract in the `Wharf.Reporting` namespace.
Expose `ReportRenderer`, `ReportInput`, `ReportSchema`, and `RetryReport`,
while preserving the existing `Wharf.Configuration.RunConfiguration` and
`Wharf.Configuration.WharfConfiguration` types.

Use these historical public shapes:

- `ReportSchema.Version`, equal to `"1.0"`;
- `RetryReport(int InfrastructureRetries, int RetryableFailures,
  int ExhaustedRetries)`;
- `RateLimitReport(int Responses429, int AffectedImages,
  int ExhaustedImages, long CumulativeDelayMilliseconds)`;
- `ReportInput(string SchemaVersion, string RunId, JsonElement Configuration,
  IReadOnlyDictionary<string, string> Provenance, AuditReport Audit,
  RetryReport Retries, RateLimitReport RateLimits)`;
- instantiable `ReportRenderer` methods `RenderMarkdown(ReportInput)`,
  `RenderJson(ReportInput)`, and `RenderJsonLines(ReportInput)`; and
- `ReportInputException` for invalid or inconsistent report input.

Reject a report when its schema version is not `ReportSchema.Version` or when
its run ID differs from the audit run ID. Rendering must be deterministic.
JSON Lines output contains one compact JSON object followed by exactly one
platform newline. Markdown uses `missing` for absent aggregate values and
renders summary values in labeled lines using these exact label formats:
`PASS@1: \`50%\``, `Cost: \`missing\``,
`Infrastructure retries: \`2\``, and `HTTP 429 responses: \`3\`` for the
corresponding sample values.

IMPORTANT: Do not commit changes. The evaluator captures the working-tree
patch after implementation.
