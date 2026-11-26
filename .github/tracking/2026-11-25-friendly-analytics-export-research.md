# Friendly analytics export: research

## Approved outcome

Default discovery is ./jobs. Find completed all-N runs, skip incomplete/resumable
runs, combine completed jobs per arm, create missing JSONL exports and leave
existing files alone unless --recompute is supplied. User explicitly chose
every completed all-N and combination, not just all-1.

## Reused and new evidence

- Reuse the [analytics inspection](2026-11-25-analytics-inspection-research.md).
  This change fixes discovery, completeness, metadata and combined identity;
  general reward/manifest semantic hardening remains separate.
- CommandTree.cs:763 currently requires output/arm/model and defaults inaccurate
  generic labels. AnalyticsExporter.cs:11 supports one job and unconditional
  replacement; DataManifestBuilder.cs:34 checks trial alone.
- Existing scripts use (job, trial) identity; keep original trial directory
  names so evidence links remain usable. Combined jobs need the same tuple
  identity in exporter and manifest.
- RetainedRunMetadata.cs:16 holds selected checksums/configuration. Native plans
  do not record subagent availability; do not invent it. Export null unless
  explicitly supplied. Existing boolean JSON remains readable.
- RetainedRunResumePlanner.cs:171 validates expected attempts, but the full
  planner loads current task payloads. Historical completed runs must export
  without requiring their current checkout to match old checksums. Reuse its
  plan-log parsing, arm parsing, trial naming and terminal identity validation
  at the evidence boundary instead.
- Local WharfSWE has 75 all-N directories and 65 run plans. Legacy discovery
  therefore needs log K/task-count and arm-name inference with terminal identity/
  checksum/count validation; missing evidence must explicitly skip.
- RunService.cs:874 finishes durable batch state; active/nonfinished latest
  invocations must not be exported even if some result files are present.
- AtomicFile.cs:5 always overwrites. No-recompute creation must be atomic and
  no-overwrite, including a competing writer race.

## Recommendation

Add a small discovery/coordinator beside the existing exporter. Stop traversal
at all-N directories and do not follow directory symlinks. Group by parent arm,
resolve sibling suite data output, skip existing output before evidence reading,
then validate all eligible jobs and combine deterministic (job, trial) rows.
Preserve explicit single-arm output/metadata overrides, rejecting contradictions.
Incomplete jobs get clear skip reasons; invalid metadata fails explicitly.

Keep notifications, execution, pinned tasks, raw jobs and existing exports
unchanged. Do not auto-rebuild manifests or launch paid work.

## Implement discovery: requested latest is not a conflicting effective pin

The first real-job scratch validation stopped at
`wharf-swe/jobs/copilot-gpt-6-astra-default-1.0.108/all-1`: its older native plan
records cliVersion latest and omits cli (the documented Copilot default).
The resolved three-part version is in the arm directory. Treat latest as the
request and infer the effective version from that directory, emitting a warning.
Explicit contradictory version pins still fail. No retained plan is rewritten.

After that compatibility correction, real discovery exported all 75 completed
WharfSWE jobs/arms, 4,200 trials, into scratch output only. All 12,761 inspected
source-evidence SHA-256 hashes remained unchanged. All rows retained agent/trial
timing; cost and full-checksum coverage were each 3,640/4,200. Legacy missing
checksums and unknown subagent availability were disclosed, not fabricated.
