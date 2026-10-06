# LLM Requirements JSONL Format

Use this format for the machine-oriented companion to a PRD when the source
contains many atomic requirements or when requirements need filtering,
traceability, retrieval, or automated validation.

## Common fields

Every record uses:

```json
{"record_type":"requirement","id":"REQ-001","text":"Exact atomic claim.","status":"stated","priority":"must","source_refs":[{"path":"notes.md","heading":"Run configuration"}],"tags":["scheduler"],"depends_on":[],"relates_to":["GOAL-001"],"acceptance_criteria":["AC-001"]}
```

Required common fields:

- `record_type`: one of `metadata`, `goal`, `requirement`, `constraint`,
  `decision`, `non_goal`, `acceptance_criterion`, `deliberate`, or
  `open_question`.
- `id`: stable unique identifier. The metadata record uses `META-001`.
- `text`: complete atomic claim with conditions and bounds preserved.
- `status`: `stated`, `derived`, `deliberate`, or `open`.
- `source_refs`: array of `{path, heading}` objects. It is empty only for an
  open question whose gap spans the entire source set.
- `tags`: stable topic labels.
- `depends_on`: IDs that must be satisfied first.
- `relates_to`: related requirement or goal IDs.

Requirements and constraints also include:

- `priority`: `must`, `should`, or `could`.
- `acceptance_criteria`: IDs of verifying criteria.

Acceptance criteria also include:

- `verifies`: requirement or goal IDs.
- `verification`: a checkable method, command, test, or observation.

Open questions also include:

- `blocking`: boolean.
- `owner`: decision owner when stated, otherwise `null`.
- `resolution_path`: where or how the answer should be recorded.
- `proposed_assumption`: suggested default or `null`.

The metadata record also includes:

- `schema_version`;
- `product`;
- `generated_at`;
- `source_files` with SHA-256 hashes;
- `human_prd`;
- `readiness_review`; and
- counts by record type and status.

## Rules

- Write one compact JSON object per physical line.
- Do not wrap records in an array.
- Do not include Markdown comments or code fences in the output file.
- Keep IDs stable across regeneration when the underlying claim is unchanged.
- Split compound bullets into atomic records without losing qualifiers.
- Do not infer priority from source order.
- Do not include a proposed assumption as a requirement until a source confirms it.
- Do not omit rejected alternatives when they constrain future decisions; store
  them as `decision` or `non_goal` records.
- Sort by record type in this order: metadata, goal, non_goal, constraint,
  decision, requirement, acceptance_criterion, deliberate, open_question.
- Sort by ID within each record type.
- Validate every line as JSON, all IDs as unique, and all ID references as existing.
