# WharfSWE analytics refresh review

Outcome: accepted against the research and completed plan.

## Executed validation

- Canonical export: ten arms, 56 unique logical trials each, 14 tasks at K=4.
  All ten plans share task IDs/checksums; every effective CLI image version
  matches the retained requested version.
- Evidence reconciliation: 560 new rows, 536 passes, 24 reward-zero results;
  no retry directories counted as extra logical trials. Retained costs match
  credits divided by 100. Agent durations sum retained agent results; trial
  durations agree with exact event spans within half a millisecond.
- Whole-dataset validation: 2632 unique identities; complete reward, test,
  cost, checksum, language, patch-size, and timing measurements. No exception
  categories. Every task has four attempts per arm. All 47 manifest aggregates,
  byte counts, score fractions, and rounded metrics match their JSONL.
- README: all 47 sorted result rows and all 57 latest-comparison metric cells
  match exports and run events. Latest plans match apart from model; there
  are no whole-trial/verifier retries in these three arms.
- SVG validation: XML parses and all 123 plotted points match exported
  pass counts, per-costed-trial costs, and inclusive p90 agent durations.
- Preservation: all 37 previously tracked exports match HEAD byte-for-byte.
- Determinism: rebuilding the manifest and regenerating all three charts
  produces byte-identical files.
- `git diff --check`: passed.

Initial scratch validation exposed an overly strict assumption that historical
task checksums were uniform. Seven existing revision-95 arms form a different
checksum cohort; they were preserved and the comparison caveat was added.
Scratch assertions were also corrected for Python view equality, exact decimal
arithmetic, and seven-digit timestamp precision before the complete successful
validation. No exporter or runtime changes were needed.

Build and runtime tests were not run: this task changes only exported data,
generated SVGs, and documentation. No benchmark was started. No unresolved
follow-ups.
