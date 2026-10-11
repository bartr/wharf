The Wharf 0.15.0 release has audit results but no deterministic report
rendering. Implement the 0.16.0 reporting slice.

Render schema-versioned Markdown, indented JSON, and one-record JSON Lines from
the same input. Include PASS@1, enterprise score, completeness, coverage,
regression, cost, token, timing, retry, and registry-rate-limit evidence.
Missing optional measurements must remain visibly missing rather than becoming
zero. Identical inputs must produce identical output. Reject unsupported
schemas and disagreements between report and audit run IDs.

Preserve the historical public contract in the `Wharf.Reporting` namespace.
Expose `ReportRenderer`, `ReportInput`, `ReportSchema`, and `RetryReport`,
while preserving the existing `Wharf.Configuration.RunConfiguration` and
`Wharf.Configuration.WharfConfiguration` types.

Do not modify existing tests or commit changes. The evaluator captures the working-tree
patch after implementation.
