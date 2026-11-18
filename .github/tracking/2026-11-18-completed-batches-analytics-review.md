# Completed-batch analytics review

## Outcome

Accepted against the completed-batches analytics plan.

## Executed validation

* `python3 -m unittest discover -s tests -p test_estimate_wharf_swe_codex_costs.py`:
  four tests passed. Cases cover cached/uncached input, output/reasoning
  accounting, whole-trial/infrastructure retries, incomplete usage,
  unsupported cache accounting, invalid counts, and exported-total mismatches.
* Regenerated estimates and repeated enrichment: byte-identical results.
* Rebuilt `wharf-swe/data/manifest.json` with the canonical CLI: 59 arms,
  3,304 logical trials.
* Read-only validation ran in named tmux session
  `wharf-analytics-refresh-20261118`, foreground output connected to its pane.
  Final `VALIDATION_EXIT=0`.
* All 59 exports: 56 distinct logical trials, 14 tasks, four attempts per
  task, one consistent provider/model/version/effort, reward totals,
  P2P-trial penalties, byte sizes, retained costs, mean costs, and durations
  reconcile with the manifest. Copilot credit conversion reconciles exactly.
* All four refreshed arms: 224 trial rows reconcile with retained plans,
  task checksums, language, identities, raw verifier rewards/test counts,
  patch sizes, exceptions, and accepted-invocation stage spans.
  Agent durations reconcile with 225 retained execution summaries.
* All 169 Codex execution trajectories reconcile with summary token counts.
  All 168 trial estimates independently reconcile with the published rates,
  cache discounts, output usage, and retry sums. Codex billed costs remain null.
* Existing Luna canonical fields are unchanged from HEAD.
* All 59 full README result rows and seven latest-result rows reconcile
  with manifest values; full-table ordering and estimate means verified.
* Each of three SVGs contains exactly the expected 49 Copilot-only arm
  identities; pass counts, mean retained costs, and inclusive p90 durations
  independently reconcile with the JSONL.
* Hashes of inspected raw plan/state/stage/agent/verifier records were
  unchanged throughout validation. No Copilot JSONL or DeepSWE changes.
* `git diff --check` passed.

The initial ad-hoc validator used Python integer `/` in one historical
credit-conversion assertion, introducing a float comparison. Corrected the
validator to use Decimal division; no exported data required a change.

No build or full .NET suite was needed: changes are offline Python tooling,
portable analytics data, and documentation. No paid model calls were made.

## Limitations

Codex estimates are Standard short-context API-equivalent scenarios, not
subscription charges or exact invoices. Aggregated usage cannot establish
per-request context lengths. Long-context and other pricing tiers are not
inferred. Missing execution usage leaves the whole-trial estimate null.
Rejected quota usage stays in its separate audit, outside accepted-trial costs.
Existing nonzero agent exits remain explicit even when verification passes.
