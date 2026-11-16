# WharfSWE analytics refresh research

The existing export and scoring contracts are adequate; no runtime changes are
needed. Reused evidence:

- `docs/ANALYTICS.md:7` defines canonical one-row-per-logical-trial exports,
  retained failures, and explicit cost coverage.
- `docs/ANALYTICS.md:52` defines the regression-adjusted score.
- `src/wharf/Reporting/RunEvidenceReader.cs:36` reads terminal results;
  line 144 collects retained agent retry evidence.
- `scripts/plot-wharf-swe-pass-at-1.py:27` validates K=4 over 14 tasks and
  regenerates the three charts from JSONL.
- `wharf-swe/README.md:99` documents existing coverage, and line 123 defines
  the comparison table columns.

Read-only inspection found ten local `all-1` runs absent from the existing
37-arm export: Luna 1.0.109 and HydraFusion, Sol, and Luna 1.0.110 through
1.0.112. Each has 56 terminal result files and final cleanup events. Their
retained plans specify K=4/N=40/P=16/default effort. The prior 1.0.109
HydraFusion/Sol comparison uses P=24, so cross-version claims must retain the
host/preparation caveat.

Recommended approach: use the installed canonical `wharf analytics export`,
rebuild only the WharfSWE manifest, regenerate charts, and refresh README
coverage, results, and latest-version comparison. Validate every new row
against retained result, reward, image-version, patch, timing, and task-plan
evidence. Preserve raw jobs and existing exports; do not touch DeepSWE.
