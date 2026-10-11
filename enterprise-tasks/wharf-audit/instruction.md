The Wharf 0.14.0 release has run operations but no audit scoring boundary.
Implement the 0.15.0 audit and completeness slice.

Score retained logical attempts without discarding missing, invalid, partial,
or explicit-zero measurements. PASS@1 uses only the first attempt for each
expected task. Enterprise score combines pass outcome and new-test coverage,
and an existing-test regression forces the affected attempt score to zero.
Aggregate cost only when cost evidence is complete. Report task, attempt, and
measurement completeness separately so absent evidence cannot look successful.

Reject duplicate or unexpected task records and malformed attempt numbering
explicitly. Keep scoring deterministic regardless of input task order.

Preserve the historical public contract in the `Wharf.Audit` namespace.
Expose `AuditScorer`, `AuditRun`, `AuditReport`, and `MeasurementState`, while
preserving the existing `Wharf.Configuration.RunConfiguration` and
`Wharf.Configuration.WharfConfiguration` types.

Do not modify existing tests or commit changes. The evaluator captures the working-tree
patch after implementation.
