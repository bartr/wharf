# Wharf 1.1.0

Wharf 1.1.0 builds on the first MVP release of the customer-controlled benchmark
execution platform. It provides deterministic DeepSWE task selection, isolated
Copilot agent and verifier execution, durable evidence, recovery, unattended
benchmark operation, HydraFusion support, and reproducible analytics.

New runs default to `hydrafusion` with no explicit reasoning effort, leaving
effort selection to the model. Configure a different model with `--model` (`-m`),
`WHARF_MODEL`, or manifest `model`. Effort defaults to `default`, which omits
the Copilot CLI effort argument.
Use `--effort` (`-e`) to configure effort. Both aliases are also available for
`wharf analytics export`.

Use single-dash concurrency options: `wharf run -k 4 -n 32 -p 12`.
For resume, use `-n` and `-p` to override retained concurrency; K remains fixed.
The double-dash forms `--k`, `--n`, and `--p` are not accepted.

See [1.1.0 release notes](docs/RELEASE-NOTES-1.1.0.md) for migration details,
including the removed double-dash concurrency flags and changed defaults.
Qualification records are in
[`docs/release-qualification.md`](docs/release-qualification.md). Historical
benchmark summaries and their retained job directories remain separate
evidence; they are not reruns of every benchmark with the 1.1.0 binary.

## Release highlights

* Real non-dry-run benchmark execution through the integrated Wharf path.
* Container-backed smoke validation and mountless agent/verifier execution.
* Correct container CLI version resolution, HydraFusion and provider-selected
  effort defaults, and model/effort short options.
* Expanded DeepSWE and enterprise benchmark evidence, with the enterprise
  dataset retaining 24 K=4 arms and 1,344 logical trials.
* Durable patches, logs, trajectories, verifier evidence, retries, timing,
  usage, cost, and recovery state.
* Planned-denominator PASS@1, separate task accuracy, and portable JSONL
  analytics with manifest validation.
* Richer analytics manifests with run identity, cost/time summaries, and a
  separately labeled regression-adjusted score.

## Data Analytics Repro Guide

This file documents how to reproduce the benchmark comparisons from `data/*.jsonl` with the same metric definitions used in this repo.

## Current benchmark results

Non-HVE runs, sorted by PASS@1 descending, then P2P errors ascending, then
cost ascending.
PASS@1 uses the fixed planned attempt universe; Passes shows passing/planned
trials. P2P Errors counts retained trials with at least one measured
regression-test failure, not individual failing tests. Unmeasured P2P results
are unknown, not confirmed regression-free trials. Cost/task means cost per
costed task attempt: known cost divided by costed trials, with no imputation.
The Cost column uses this definition. Agent Time is the task-weighted mean
per-attempt agent-command duration, rounded to whole seconds; it excludes
preparation and verification.

| Model | Passes | PASS@1 | P2P Errors | Cost | Agent Time |
| --- | ---: | ---: | ---: | ---: | ---: |
| Copilot GPT-5.6 Luna Max 1.0.77 | 335/452 | 74.12% | 17 | $0.3711 | 1511 s |
| Copilot Gemini 3.8 Flash High 1.0.91 | 335/452 | 74.12% | 20 | $2.7897 | 1879 s |
| Copilot Claude Opus 5 Max 1.0.77 | 322/452 | 71.24% | 20 | $5.6623 | 2146 s |
| Copilot GPT-6 Astra Max 1.0.91 | 321/452 | 71.02% | 43 | $8.4274 | 1619 s |
| Copilot Claude Opus 5.5 Max 1.0.95 | 320/452 | 70.80% | 22 | $11.9774 | 3765 s |
| **HydraFusion 1.0.98 All-1** | **318/452** | **70.35%** | **28** | **$1.2149** | **1069 s** |
| **HydraFusion 1.0.95 All-2** | **317/452** | **70.13%** | **32** | **$1.1726** | **1466 s** |
| Copilot GPT-5.6 Luna Max 1.0.91 | 316/452 | 69.91% | 21 | $0.3670 | 1614 s |
| Copilot GPT-5.6 Sol XHigh 1.0.77 | 315/452 | 69.69% | 23 | $8.4366 | 1823 s |
| **HydraFusion 1.0.95 All-1** | **311/452** | **68.81%** | **36** | **$1.2074** | **1424 s** |
| Copilot Claude Opus 5.5 Max 1.0.91 | 309/452 | 68.36% | 27 | $11.4134 | 3452 s |
| Copilot GPT-5.6 Terra Max 1.0.77 | 376/565 | 66.55% | 37 | $4.2576 | 1677 s |
| Claude Code Opus 5 Max 2.1.220 | 291/452 | 64.38% | 21 | $13.4885 | 1873 s |
| Copilot GPT-6 Luna Max 1.0.91 | 276/452 | 61.06% | 36 | $0.1759 | 1417 s |
| Muse Spark 1.2 Contributor XHigh 0.1.0 | 338/565 | 59.82% | 44 | N/A | 1185 s |
| Copilot Auto Max 1.0.77 | 209/565 | 36.99% | 48 | $1.6234 | 1308 s |

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

Each manifest entry includes the arm's model, effort, CLI and CLI version,
passing trial count, P2P error count, cost per costed trial, and average
agent-command time. It also retains trial/task totals, scored count, total
cost, cost coverage, and JSONL byte size. `passes` counts trials with
`reward > 0`; `p2p_errors` counts trials where the retained regression tests
have fewer passes than tests; cost per trial excludes rows without cost; and
average agent time averages rows with agent duration evidence. A JSONL file
must contain a single consistent model, effort, CLI, and CLI version.

The manifest also includes `score = passes / trials - (p2p_errors * 0.0025)`.
Each P2P error subtracts 0.25 percentage points from the trial pass rate.
The score is stored as a fraction, displayed as a percentage, and is not
clamped at zero. Empty arms have a null score. This regression-adjusted score
is separate from PASS@1.

Export separate jobs to separate files when they must remain independently
auditable. Merge only when the comparison contract defines them as one arm.

The Muse export keeps per-trial `cost_usd` null. Its dashboard bill is the
authoritative `$34.62` batch cost. The ranking leaves cost/task unavailable
rather than allocating the batch bill into missing per-trial cost evidence. See
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
