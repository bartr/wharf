# Copilot 1.0.113 WharfSWE analytics review

## Outcome: accepted

Reviewed against the research and completed plan in
`2026-11-17-copilot-113-analytics-plan.md`. Implementation and validation
evidence is recorded in `2026-11-17-copilot-113-analytics-changes.md`.

* Five requested completed Copilot arms exported, 56 trials each.
* Logical identities, K depth, task checksums, rewards, test counts, costs,
  actual agent-command durations, trial spans, patch bytes, and exception
  coverage reconciled against retained evidence.
* PASS@1 uses all retained trials. P2P penalties count affected trials.
  Costs use cost-bearing trials only; all new arms have 100% coverage.
* Manifest totals are 52 arms / 2,912 trials; all result-table values and
  sorting match. Latest comparison is labeled 1.0.113; 1.0.112 is historical.
* All three SVGs parse and match exported pass counts, mean cost per costed
  trial, and inclusive-p90 agent durations for all 46 plotted arms.
* Historical JSONL exports are byte-identical to HEAD. Scope excludes
  DeepSWE, raw jobs, pending Claude Code recovery, and user-owned run script.
* Whitespace validation passed. Product build/tests are not applicable:
  this change contains generated analytics and documentation only.

Execution completed and accepted. No defects, decision gaps, or residual
work identified. Changes remain uncommitted as requested scope did not
include a commit.
