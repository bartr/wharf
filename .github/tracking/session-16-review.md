# Session 16 Review

- Rendering is pure over a retained `ReportInput`; no process or wall-clock
  state is consulted.
- JSONL emits exactly one report object per line.
- Invalid schema or mismatched run identity fails explicitly instead of
  producing a success-shaped report.
