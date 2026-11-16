# WharfSWE analytics refresh changes

- Exported ten completed `all-1` arms with installed Wharf 1.5.0:
  GPT-6 Luna 1.0.109, and HydraFusion/GPT-6.1 Sol/GPT-6 Luna 1.0.110-112.
  Used canonical `wharf analytics export` with explicit arm, model, CLI
  version, default effort, subagents enabled, and job `all-1`.
- Added 560 logical trials, including all 24 reward-zero trials. Their 536
  passing trials remain measured against all 560 trials, with no exclusions.
- Rebuilt `wharf-swe/data/manifest.json` using
  `wharf analytics manifest --data-directory wharf-swe/data`.
- Regenerated all three charts using
  `python3 scripts/plot-wharf-swe-pass-at-1.py`. Each has 41 observed arms.
- Updated README coverage to 47 arms / 2632 trials, inserted all ten sorted
  results, and replaced the latest comparison with the three 1.0.112 arms.
- Documented existing historical checksum differences rather than changing
  seven revision-95 exports or claiming the entire history is checksum-matched.

Validation passed: all 560 new rows reconcile with retained plans, effective
CLI image metadata, terminal identities/statuses, reward/test counts, credits,
USD conversion, patch sizes, agent durations including retries, and trial
event spans. All 47 manifest entries, 47 README results rows, 57 latest-table
metric cells, and 123 SVG points reconcile. All 37 prior exports are
byte-identical to HEAD. No retained job evidence or DeepSWE data was changed.
