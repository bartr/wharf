# Scoring core plan

1. [x] Update `AuditContracts.cs` and `AuditScorer.cs`.
   - Compute PASS@1 from passing/observed logical attempts.
   - Add task pass-at-least-once and explicit Enterprise coverage/pass components.
   - Preserve expected-task-equal Enterprise weighting.
   - Exit: focused scorer tests cover unequal K, missing tasks, and component sum.
2. [x] Update `ReportContracts.cs` and `ReportRenderer.cs`.
   - Render the new trial and task metrics plus Enterprise components in Markdown
     and machine formats.
   - Exit: focused renderer tests assert unambiguous labels and serialized fields.
3. [x] Add `Reporting/AnalyticsContracts.cs`, `RunEvidenceReader.cs`, and
   `AnalyticsExporter.cs`.
   - Produce deterministic one-row-per-logical-trial canonical JSONL from native
     evidence, summing agent time across whole-trial retries without duplicating rows.
   - Preserve nulls for unavailable evidence and convert AI Credits at $0.01.
   - Exit: fixture tests validate identity, metrics, retry timing, cost, and ordering.
4. [x] Add `Reporting/DataManifestBuilder.cs`.
   - Read only `*.jsonl` exports and atomically build the canonical manifest.
   - Exit: tests prove jobs/raw evidence is unnecessary and summaries are correct.
5. [x] Run only the changed-area tests and record results in the changes/review
   artifacts.
