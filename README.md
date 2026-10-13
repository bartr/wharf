# Wharf 1.0.0

Wharf 1.0.0 is the first MVP release of the customer-controlled benchmark
execution platform. It provides deterministic DeepSWE task selection, isolated
Copilot agent and verifier execution, durable evidence, recovery, unattended
benchmark operation, HydraFusion support, and reproducible analytics.

The release is qualified by retained local summaries in `data/` and the
corresponding Wharf and Pier job directories maintained with the benchmark
evidence. The final acceptance record is in
[`docs/release-qualification.md`](docs/release-qualification.md).

## Release highlights

* Real non-dry-run benchmark execution through the integrated Wharf path.
* Container-backed smoke validation and mountless agent/verifier execution.
* Two unattended HydraFusion benchmark runs, including overnight execution
  where applicable.
* Durable patches, logs, trajectories, verifier evidence, retries, timing,
  usage, cost, and recovery state.
* Planned-denominator PASS@1, separate task accuracy, and portable JSONL
  analytics with manifest validation.

## Data Analytics Repro Guide

This file documents how to reproduce the benchmark comparisons from `data/*.jsonl` with the same metric definitions used in this repo.

## Current benchmark results

PASS@1 uses the fixed planned attempt universe. Cost/test divides known cost by
costed trials rather than treating missing cost as zero.

| Rank | Test | PASS@1 | Cost/test |
| ---: | --- | ---: | ---: |
| 1 | Copilot Gemini 3.8 Flash High 1.0.91 | 74.12% | $2.7897 |
| 2 | Copilot GPT-5.6 Luna Max 1.0.77 | 74.12% | $0.3711 |
| 3 | Copilot Claude Opus 5 Max 1.0.77 | 71.24% | $5.6623 |
| 4 | Copilot GPT-6 Astra Max 1.0.91 | 71.02% | $8.4274 |
| 5 | Copilot Claude Opus 5.5 Max 1.0.95 | 70.80% | $11.9774 |
| 6 | **HydraFusion 1.0.95** | **70.13%** | **$1.1726** |
| 7 | Copilot GPT-5.6 Luna Max 1.0.91 | 69.91% | $0.3670 |
| 8 | Copilot GPT-5.6 Sol XHigh 1.0.77 | 69.69% | $8.4366 |
| 9 | **HydraFusion 1.0.95** | **68.81%** | **$1.2074** |
| 10 | Copilot Claude Opus 5.5 Max 1.0.91 | 68.36% | $11.4134 |
| 11 | Copilot GPT-5.6 Terra Max 1.0.77 | 66.55% | $4.2576 |
| 12 | **Claude Code Opus 5 Max 2.1.220** | **64.38%** | **$13.4885** |
| 13 | Copilot GPT-6 Luna Max 1.0.91 | 61.06% | $0.1759 |
| 14 | Muse Spark 1.2 Contributor XHigh 0.1.0 | 59.82% | $0.0613 |
| 15 | Copilot Auto Max 1.0.77 | 36.99% | $1.6234 |

## 1) Canonical analytics rules

Use these definitions consistently:

- Row unit: one trial (`data/<arm>.jsonl` is one JSON object per trial).
- PASS@1: passing attempts / (`K` × 113). Missing scheduled attempts count as
  non-passes and never reduce the denominator.
- Task solved (for accuracy): task counts as solved if **any** trial for that task has `reward > 0`.
- Language accuracy: solved tasks / tasks seen in that language.
- Cost per task: total `cost_usd` across that language / number of unique tasks in that language.
- Runtime headline metric (required): **agent-only**, k-normalized per task.
  - Per task: `task_agent_seconds = sum(agent_seconds for task) / k_task`.
  - Summary for language/arm: average `task_agent_seconds` across tasks (unweighted by attempts).
- Validator-inclusive wall time (`trial_seconds`) is optional supporting context, not the primary performance metric.

## 2) Clean separation: benchmarking vs reporting

This repo now treats these as separate phases:

- Benchmark/export phase (needs `jobs/`): produce `data/<arm>.jsonl` from raw trial dirs.
- Reporting phase (does NOT need `jobs/`): compute summaries from `data/*.jsonl` only.

If you delete `jobs/` after exporting JSONL, reporting should still work.

## 3) Data export format and regeneration

Current export format:
- analytics files are plain `data/<arm>.jsonl` (not `.jsonl.gz`)
- index file is `data/manifest.json`
- manifest size key is `bytes_jsonl`

Export each retained Wharf run directly:

```bash
wharf analytics export \
  --run-directory jobs/<arm>/<job> \
  --output data/<arm>.jsonl \
  --arm <arm> \
  --agent copilot-cli \
  --agent-version <version> \
  --model <model> \
  --effort <effort> \
  --job <job>
```

The exporter emits one row per logical trial. Agent and verifier
infrastructure retries remain nested evidence and do not add rows.

To rebuild the manifest after exporting:

```bash
wharf analytics manifest --data-directory data
```

Export separate jobs to separate files when they must remain independently
auditable. Merge only when the comparison contract defines them as one arm.

The Muse export keeps per-trial `cost_usd` null. Its dashboard bill is the
authoritative `$34.62` batch cost. The ranking allocates that billed total
across the 565 retained arm trials for a comparable cost/test value. See
[ANALYTICS.md](./ANALYTICS.md) for the batch accounting.

Important:
- `--run-directory`, `--output`, `--arm`, and `--model` are required.
- `--agent`, `--effort`, and `--subagents` have explicit defaults.
- `analytics export` never rewrites `data/manifest.json`; run `analytics manifest`
  after all intended exports complete.

## 4) Reporting-only manifest rebuild (no jobs required)

Use this command to rebuild `data/manifest.json` strictly from `data/*.jsonl`:

```bash
wharf analytics manifest --data-directory data
```

The command intentionally does not read `jobs/`.

## 5) Reproduce the comparison (copy/paste)

Run from repo root:

```bash
python3 - <<'PY'
import json, collections, statistics as st
from pathlib import Path

arms=[
  'cc-claude-opus-5-max',
  'copilot-claude-opus-5-max',
]
langs=['go','python','typescript','javascript','rust']

all_rows={}
for arm in arms:
    p=Path('data')/(arm+'.jsonl')
    all_rows[arm]=[json.loads(l) for l in p.read_text().splitlines() if l.strip()]

print('BY_LANGUAGE')
print('arm\tlanguage\ttasks\tavg_k\taccuracy_pct\tcost_per_task_usd\tagent_h_per_task_norm\tclock_h_per_task_norm')
for arm in arms:
    rows=all_rows[arm]
    by_lang=collections.defaultdict(list)
    for r in rows:
        by_lang[r['language']].append(r)

    for lang in langs:
        rs=by_lang.get(lang,[])
        by_task=collections.defaultdict(list)
        for r in rs:
            by_task[r['task_id']].append(r)
        if not by_task:
            print(f"{arm}\t{lang}\t0\t0\tNA\tNA\tNA\tNA")
            continue

        solved=0
        total_cost=0.0
        k_vals=[]
        task_agent=[]
        task_clock=[]

        for tid, trs in by_task.items():
            k=len(trs)
            k_vals.append(k)
            rewards=[t.get('reward') for t in trs if t.get('reward') is not None]
            if rewards and max(rewards) > 0:
                solved += 1
            total_cost += sum((t.get('cost_usd') or 0.0) for t in trs)
            task_agent.append(sum((t.get('agent_seconds') or 0.0) for t in trs)/k)
            task_clock.append(sum((t.get('trial_seconds') or 0.0) for t in trs)/k)

        n_tasks=len(by_task)
        print(
            f"{arm}\t{lang}\t{n_tasks}\t{st.mean(k_vals):.2f}\t"
            f"{100*solved/n_tasks:.1f}\t{total_cost/n_tasks:.2f}\t"
            f"{st.mean(task_agent)/3600:.2f}\t{st.mean(task_clock)/3600:.2f}"
        )

print('\nOVERALL')
print('arm\ttasks\tavg_k\taccuracy_pct\tcost_per_task_usd\tagent_h_per_task_norm\tclock_h_per_task_norm')
for arm in arms:
    rows=all_rows[arm]
    by_task=collections.defaultdict(list)
    for r in rows:
        by_task[r['task_id']].append(r)

    solved=0
    total_cost=0.0
    k_vals=[]
    task_agent=[]
    task_clock=[]

    for tid, trs in by_task.items():
        k=len(trs)
        k_vals.append(k)
        rewards=[t.get('reward') for t in trs if t.get('reward') is not None]
        if rewards and max(rewards) > 0:
            solved += 1
        total_cost += sum((t.get('cost_usd') or 0.0) for t in trs)
        task_agent.append(sum((t.get('agent_seconds') or 0.0) for t in trs)/k)
        task_clock.append(sum((t.get('trial_seconds') or 0.0) for t in trs)/k)

    n_tasks=len(by_task)
    print(
        f"{arm}\t{n_tasks}\t{st.mean(k_vals):.2f}\t{100*solved/n_tasks:.1f}\t"
        f"{total_cost/n_tasks:.2f}\t{st.mean(task_agent)/3600:.2f}\t{st.mean(task_clock)/3600:.2f}"
    )
PY
```

## 6) Repro gotchas

- Do not compare wall/task without k-normalization; it mostly measures depth (`k`) rather than per-attempt efficiency.
- `subagents` in these exports is boolean (`True`/`False`), not per-trial call counts.
- JS and Rust have only 5 tasks each, so language-level percentages are higher variance.
- Keep task universe at 113 benchmark tasks. `hello-go` and `hello-py` are harness smoke tests, not benchmark tasks.
