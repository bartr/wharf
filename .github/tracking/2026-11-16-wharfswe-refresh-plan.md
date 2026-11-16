# WharfSWE analytics refresh plan

Based on `2026-11-16-wharfswe-refresh-research.md`; no implementation decisions
remain open.

1. [x] Export the ten completed local arms into `wharf-swe/data/` using
   explicit retained model, CLI version, default effort, subagents, and job.
   Exit: 560 new rows, 14 tasks and four attempts each, matching raw evidence.
2. [x] Rebuild `wharf-swe/data/manifest.json` with the canonical CLI and
   regenerate `wharf-swe/docs/` charts using the existing plotting script.
   Exit: 47 arms, 2632 trials, correct manifest aggregates and chart points.
3. [x] Update `wharf-swe/README.md` coverage, sorted results, and latest
   1.0.112 comparison from measured evidence.
   Exit: all displayed metrics reconcile with exports and retained timings.
4. [x] Validate unique identity, task/checksum coverage, K, rewards, costs,
   measurements, durations, manifest entries, README rows, and SVG structure.
   Record execution and review evidence in adjacent tracking artifacts.

Out of scope: runtime changes, new benchmarks, changing retained jobs or old
exports, DeepSWE analytics, commits, and releases.
