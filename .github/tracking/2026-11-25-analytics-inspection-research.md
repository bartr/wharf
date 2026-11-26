# Analytics inspection

## Decision: retain the capability, harden validation next

Analytics is real functionality, not a placeholder. CommandTree's analytics
export and manifest handlers invoke AnalyticsExporter and DataManifestBuilder.
Existing tests cover retry aggregation, credits versus dollar costs, unknown
costs, deterministic rows, and regression-adjusted manifest scores.

It was not modified as part of removing unused commands. The findings below
are validation gaps, not evidence that current retained benchmark scores are
wrong. Audit specific arms before making that claim.

## What works

- AnalyticsExporter.cs:11 exports native retained evidence, sorts trial IDs,
  rejects duplicate trial strings and atomically replaces JSONL output.
- RunEvidenceReader.cs:148 includes whole-trial and infrastructure retry agent
  evidence in one trial row. Verifier retries do not create extra agent rows.
  :61 respects explicit terminal cost nulls; :94 converts credits at $0.01.
- DataManifestBuilder.cs:8 reads only portable exports, rejects mixed arms and
  mixed non-null CLI/version/model/effort labels, counts all retained rows in
  its score denominator, and computes cost only from costed rows.
- DataManifestBuilderTests covers negative unclamped scores and empty null
  scores. Manifest score is separate from planned-universe DeepSWE PASS@1.

## Reproduced gaps

All reproductions used synthetic scratch fixtures and the compiled Release
CLI, with an isolated HOME. No Docker, provider, paid evaluation or benchmark
evidence was touched. Temporary fixtures/output were removed after recording.

1. **Completeness is not enforced.** RunEvidenceReader.cs:35-42 enumerates
   existing trial directories and skips ones without result.json. It does not
   compare exported identities to the retained K/task plan or require run
   completion. A fixture declaring K=2 and two tasks, with one terminal result
   and one incomplete directory, exported one row with exit 0 and no warning.
   This can conceal missing scheduled work. Preserve planned-universe scoring
   separately; do not infer a complete evaluation from a successful export.
2. **Caller labels override experiment evidence without cross-checking.**
   RunEvidenceReader.cs:84-88 stamps agent/version/model/effort/subagent values
   from export options; :366 checks only nonempty required labels. The same
   fixture declared retained-model/max/1.0.116, but export accepted
   supplied-model/low/9.9.9 and wrote those labels with exit 0. CLI defaults
   also assume copilot-cli/max/subagents=true. Derive retained settings where
   available, and reject contradictory overrides rather than relabel results.
3. **Verifier evidence can be replaced by status-derived reward.**
   RunEvidenceReader.cs:224-233 maps status passed to reward 1 when reward.json
   is absent. The synthetic result exported reward 1 with null F2P/P2P counts
   and no verifier reward file. This may be intended legacy compatibility, but
   it cannot prove a verified pass. A hardening decision must specify when
   legacy evidence is admissible and expose missing measurements explicitly.
4. **Manifest validation is structural, not semantic.**
   DataManifestBuilder.cs:105 deserializes rows without required-field/range
   checks; :47 counts any positive reward as passing. A row with reward 2,
   cost_usd -1 and agent_seconds -5, omitting model/CLI metadata, produced a
   manifest with passes=1, score=1, cost_per_trial_usd=-1 and
   average_agent_seconds=-5, exit 0. Validate identities, rewards, counts,
   costs, duration ranges and evidence coverage before output creation.

## Existing compatibility boundary

docs/ANALYTICS.md:44 already records that the historical Claude Code Opus arm
contains three effective CLI versions and blocks a full DeepSWE manifest
rebuild under the single-version-per-arm rule. Do not rewrite versions, drop
trials or regenerate that manifest merely to bypass the rejection.

## Next evaluation

Return to Plan for analytics hardening: define incomplete-run handling,
retained-versus-supplied metadata precedence, legacy reward admissibility,
semantic JSONL validation and compatibility for historically mixed versions.
Use focused regression cases that currently reproduce these gaps. Keep raw
evidence, logical-trial accounting, cost coverage and both suite boundaries
unchanged. No live provider effectiveness or speed benefit was tested.
