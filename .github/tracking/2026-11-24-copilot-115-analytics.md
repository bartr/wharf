# Copilot 1.0.115 analytics refresh

## Research

- The three new `all-1` runs are HydraFusion, GPT-6.1 Sol, and GPT-6 Luna.
  Each retained run plan specifies CLI 1.0.115, default effort, K=4/N=56/P=16
  (`wharf-swe/jobs/copilot-{hydrafusion,gpt-6.1-sol,gpt-6-luna}-default-1.0.115/all-1/run-plan.json:1`).
- Read-only terminal-result validation found 56 unique task/attempt identities
  per arm, 14 tasks with four attempts each, and no trial or verifier retries.
  Terminal passes are respectively 53, 55, and 51.
- Use the canonical exporter and manifest builder, preserving failures, null
  costs, timing, checksums, and test evidence (`docs/ANALYTICS.md:8`).
- Regenerate the existing three SVG charts, which discover Copilot JSONL
  exports and validate arm shape (`scripts/plot-wharf-swe-pass-at-1.py:26`).
- Update latest results, coverage, and the sorted historical table
  (`wharf-swe/README.md:48`, `wharf-swe/README.md:153`).
- Exclude DeepSWE data, older exports, raw run evidence, and runtime changes.

## Plan

1. [x] Export exactly three arms with explicit model, version, effort, job,
   and subagent metadata. Verify 56 unique logical trials per export against
   retained task IDs/checksums, terminal results, tests, cost, and timing.
2. [x] Rebuild only the WharfSWE manifest. Check each new entry against its
   export and calculate updated aggregate coverage.
3. [x] Regenerate the existing charts and refresh the README using verified
   manifest metrics, preserving historical results and comparison caveats.
4. [x] Validate README tables and coverage against the portable data, SVG
   revision-115 points against exports, and whitespace. Record outcomes here.

## Changes and review

- Canonical export produced 56 rows for each new arm; the rebuilt WharfSWE
  manifest contains 62 arms and 3472 logical trials.
- Verified every new trial against retained terminal status, reward and test
  counts, task/checksum identity, patch byte count, credits/USD conversion,
  agent-command duration, and stage-event elapsed trial duration. All 168
  have complete cost, test, and duration evidence, no exceptions, and no
  whole-trial or verifier retries.
- GPT-6.1 Sol: 55/56 passes, zero P2P errors, 98.21% PASS@1 and Score,
  $0.1958 per costed trial, 313.6 seconds average agent time.
- HydraFusion: 53/56 passes, zero P2P errors, 94.64% PASS@1 and Score,
  $0.3070 per costed trial, 458.1 seconds average agent time.
- GPT-6 Luna: 51/56 passes, three P2P errors, 91.07% PASS@1, 90.32% Score,
  $0.0148 per costed trial, 209.2 seconds average agent time.
- Existing chart generator completed successfully: 52 plotted runs across
  five models in each of the three SVGs. XML validation confirmed exactly
  three revision-115 points per chart with matching passes and coverage.
- All 62 historical README rows match manifest identities and metrics;
  PASS@1/P2P/cost ordering is correct. The seven latest arms have matching
  task IDs/checksums. Total cost coverage is 3304/3472 (95.16%); complete
  test and duration coverage is 3472/3472. Exception count remains 53.
- Initial ad hoc checks incorrectly compared unrounded totals to rounded
  manifest costs and divided integer credits using binary floating-point.
  Corrected validation to respect manifest rounding and exact Decimal
  conversion; the full evidence/documentation validation passed.
- No runtime or dependency changes; build, lint, and runtime test suites are
  not applicable. Validation used the canonical export/manifest commands,
  existing chart generator, targeted evidence/README/SVG checks, and
  `git diff --check`.
- No follow-ups or changes to DeepSWE analytics or retained raw evidence.
