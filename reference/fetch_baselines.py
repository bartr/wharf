#!/usr/bin/env python3
# Export a DeepSWE leaderboard arm to reference/deepswe_v1_1_<arm>.tsv.
#
# Source is the artifact the dashboard itself downloads:
#   https://deepswe.datacurve.ai/artifacts/v1.1/trials.json
# The trials page is a client-rendered SPA, so scraping its HTML yields nothing;
# this file is the same data the table renders from.
#
# Rows are per-trial, not per-task: the leaderboard metric is PASS@1 (trial-level
# pass rate), so collapsing to "x/4" loses the unit the metric is computed over.
#
#   python3 reference/fetch_baselines.py                     # default arms
#   python3 reference/fetch_baselines.py claude-opus-5:high  # specific arm
import json
import os
import sys
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
ARTIFACT = "https://deepswe.datacurve.ai/artifacts/v1.1/trials.json"
CACHE = os.path.join(ROOT, "reference", ".trials-v1.1.json")

# Only the DeepSWE benchmark rollouts on the published harness; the artifact also
# carries a cross-benchmark trajectory sample that is not part of the leaderboard.
SOURCE = "deep-swe"
HARNESS = "mini-swe-agent"

DEFAULT_ARMS = [
    ("claude-opus-5", "xhigh"),
    ("claude-opus-5", "max"),
    ("gpt-5-6-sol", "xhigh"),
    ("gpt-5-6-luna", "max"),
    ("gpt-5-6-terra", "max"),
]

COLUMNS = [
    "language", "task_name", "trial_name", "outcome", "reward",
    "n_agent_steps", "cost_usd",
    "n_input_tokens", "n_cache_tokens", "n_output_tokens", "peak_context_tokens",
    "agent_duration_seconds",
    "f2p_passed", "f2p_total", "p2p_passed", "p2p_total",
    "error_category", "included_in_score",
]


def load_artifact():
    if not os.path.exists(CACHE):
        with urllib.request.urlopen(ARTIFACT, timeout=120) as r:
            body = r.read()
        with open(CACHE, "wb") as f:
            f.write(body)
    with open(CACHE, "rb") as f:
        return json.load(f)


def languages():
    """Language lives in the task definitions, not in the trials artifact."""
    out = {}
    tasks = os.path.join(ROOT, "tasks")
    for name in os.listdir(tasks):
        toml = os.path.join(tasks, name, "task.toml")
        if not os.path.exists(toml):
            continue
        with open(toml, encoding="utf-8") as f:
            for line in f:
                if line.startswith("language"):
                    out[name] = line.split("=", 1)[1].strip().strip('"')
                    break
    return out


def fmt(v):
    if v is None:
        return ""
    if isinstance(v, bool):
        return "true" if v else "false"
    if isinstance(v, float):
        # Upstream JSON floats carry binary-repr noise (7.926220750000002);
        # six decimals is far below a cent and keeps sub-second durations.
        return repr(round(v, 6))
    return str(v)


def short(model):
    return model[len("claude-"):] if model.startswith("claude-") else model


def reference_name(model, effort):
    names = {
        ("claude-opus-5", "max"): "opus5_max",
        ("claude-opus-5", "xhigh"): "opus5_xhigh",
        ("gpt-5-6-sol", "xhigh"): "gpt5_6_sol_xhigh",
        ("gpt-5-6-luna", "max"): "gpt5_6_luna_max",
        ("gpt-5-6-terra", "max"): "gpt5_6_terra_max",
    }
    return names.get((model, effort), f"{short(model).replace('-', '_')}_{effort}")


def export(rows, lang, model, effort):
    keep = [r for r in rows
            if r["model"] == model and r["reasoning_effort"] == effort
            and r["source"] == SOURCE and r["harness"] == HARNESS]
    if not keep:
        raise SystemExit(f"no trials for {model} [{effort}]")
    missing = sorted({r["task_name"] for r in keep} - set(lang))
    if missing:
        raise SystemExit(f"no language for tasks: {missing}")
    keep.sort(key=lambda r: (lang[r["task_name"]], r["task_name"], r["trial_name"]))

    path = os.path.join(ROOT, "reference", f"deepswe_v1_1_{reference_name(model, effort)}.tsv")
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write("\t".join(COLUMNS) + "\n")
        for r in keep:
            vals = [lang[r["task_name"]] if c == "language" else fmt(r.get(c))
                    for c in COLUMNS]
            f.write("\t".join(vals) + "\n")
    tasks = len({r["task_name"] for r in keep})
    passed = sum(1 for r in keep if r["outcome"] == "pass")
    print(f"{os.path.relpath(path, ROOT)}: {len(keep)} trials, {tasks} tasks, "
          f"pass {passed}/{len(keep)} ({passed / len(keep):.1%})")


def main():
    arms = DEFAULT_ARMS
    if sys.argv[1:]:
        arms = [tuple(a.split(":", 1)) for a in sys.argv[1:]]
    data = load_artifact()
    print(f"artifact: {data['n_trials']} trials -- {data['scope']}")
    lang = languages()
    for model, effort in arms:
        export(data["rows"], lang, model, effort)


if __name__ == "__main__":
    main()
