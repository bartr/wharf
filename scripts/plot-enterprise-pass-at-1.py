#!/usr/bin/env python3
"""Plot passing trials, cost, and duration by Copilot CLI revision."""

import json
import re
from collections import Counter, defaultdict
from decimal import Decimal
from html import escape
from math import ceil
from pathlib import Path
from statistics import quantiles


ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT / "enterprise" / "data"
OUTPUT = ROOT / "enterprise" / "docs" / "pass-at-1-by-cli-revision.svg"
STYLES = {
    "hydrafusion": ("HydraFusion", "#d55e00", "circle", 6),
    "gpt-6.1-sol": ("GPT-6.1 Sol", "#0072b2", "triangle", 6),
    "gpt-6-astra": ("GPT-6 Astra", "#009e73", "diamond", 8),
    "gpt-6-luna": ("GPT-6 Luna", "#cc79a7", "square", 4),
    "claude-opus-5.5": ("Claude Opus 5.5", "#9467bd", "triangle", 6),
}


def summarize(path):
    rows = [
        json.loads(line, parse_float=Decimal)
        for line in path.read_text().splitlines() if line.strip()
    ]
    counts = Counter(row["task_id"] for row in rows)
    if len(counts) != 14 or set(counts.values()) != {4}:
        raise ValueError(f"{path}: expected 14 tasks with four logical trials each")
    if len({(row["job"], row["trial"]) for row in rows}) != len(rows):
        raise ValueError(f"{path}: duplicate logical-trial identity")
    if any(row["arm"] != path.stem for row in rows):
        raise ValueError(f"{path}: arm identity mismatch")
    configurations = {
        (row["model"], row["agent_version"], row["agent"], row["effort"])
        for row in rows
    }
    if len(configurations) != 1:
        raise ValueError(f"{path}: inconsistent model, CLI version, agent, or effort")
    model, version, agent, effort = configurations.pop()
    revision = re.fullmatch(r"1\.0\.(\d+)", version)
    if revision is None or agent != "copilot-cli" or effort != "default":
        raise ValueError(f"{path}: expected Copilot CLI 1.0.x at default effort")
    for field in ("cost_usd", "agent_seconds"):
        if any(
            row[field] is not None
            and (not Decimal(row[field]).is_finite() or row[field] < 0)
            for row in rows
        ):
            raise ValueError(f"{path}: {field} must be finite and nonnegative")
    costs = [row["cost_usd"] for row in rows if row["cost_usd"] is not None]
    if any(row["agent_seconds"] is None for row in rows):
        raise ValueError(f"{path}: complete agent-command duration evidence is required")
    passed = sum(row["reward"] is not None and row["reward"] > 0 for row in rows)
    return {
        "arm": path.stem,
        "model": model,
        "revision": int(revision[1]),
        "passes": passed,
        "trials": len(rows),
        "costed_trials": len(costs),
        "cost_per_trial": sum(costs, Decimal(0)) / len(costs) if costs else None,
        "agent_p90_seconds": quantiles(
            [row["agent_seconds"] for row in rows], n=10, method="inclusive"
        )[-1],
    }


def marker(shape, size, color):
    style = f'fill="none" stroke="{color}" stroke-width="2"'
    if shape == "circle":
        return f'<circle r="{size}" {style}/>'
    if shape == "square":
        return (
            f'<rect x="{-size}" y="{-size}" width="{2 * size}" '
            f'height="{2 * size}" {style}/>'
        )
    if shape == "triangle":
        points = f"0,{-size} {size},{size} {-size},{size}"
    elif shape == "diamond":
        points = f"0,{-size} {size},0 0,{size} {-size},0"
    else:
        raise ValueError(f"Unknown marker shape: {shape}")
    return f'<polygon points="{points}" {style}/>'


def format_duration(seconds):
    minutes, seconds = divmod(int(Decimal(seconds) + Decimal("0.5")), 60)
    return f"{minutes}:{seconds:02d}"


def plot(records, output, key, title, axis_label, ticks, format_tick, format_value):
    lower, upper = ticks[0], ticks[-1]
    revisions = sorted({row["revision"] for row in records})
    first, last = 95, revisions[-1]
    series = defaultdict(list)
    for row in records:
        series[row["model"]].append(row)

    def x(revision):
        return 95 + 940 * (revision - first) / max(last - first, 1)

    def y(value):
        return 480 - 380 * (value - lower) / (upper - lower)

    svg = [
        '<svg xmlns="http://www.w3.org/2000/svg" width="1100" height="730" '
        'viewBox="0 0 1100 730" role="img" aria-labelledby="title description">',
        f'<title id="title">{escape(title)}</title>',
        '<desc id="description">Selected enterprise models, with one point per completed '
        f'56-trial arm. X is the revision of CLI 1.0.x; Y shows {escape(axis_label)}. '
        f'The X axis starts at revision 95. Y axis: {escape(format_tick(lower))} '
        f'to {escape(format_tick(upper))}. '
        'Lines join observed runs only.</desc>',
        '<rect width="1100" height="730" fill="white"/>',
        '<g font-family="sans-serif" fill="#222222">',
        '<text x="550" y="34" text-anchor="middle" font-size="24" '
        f'font-weight="bold">{escape(title)}</text>',
        '<text x="550" y="60" text-anchor="middle" font-size="14">'
        f'Selected models | 14 tasks x 4 attempts per run | '
        f'Y axis: {escape(format_tick(lower))}-{escape(format_tick(upper))}</text>',
    ]
    for tick in ticks:
        position = y(tick)
        svg.extend([
            f'<path d="M95 {position:.2f}H1035" stroke="#dddddd"/>',
            f'<text x="80" y="{position + 5:.2f}" text-anchor="end" '
            f'font-size="14">{escape(format_tick(tick))}</text>',
        ])
    for revision in revisions:
        position = x(revision)
        svg.extend([
            f'<path d="M{position:.2f} 100V480" stroke="#eeeeee"/>',
            f'<text x="{position:.2f}" y="504" text-anchor="middle" '
            f'font-size="13">{revision}</text>',
        ])
    svg.extend([
        '<path d="M95 95V480H1040" fill="none" stroke="#777777"/>',
        '<text x="565" y="535" text-anchor="middle" font-size="16">'
        'Copilot CLI revision (1.0.x)</text>',
        '<text transform="translate(30 290) rotate(-90)" text-anchor="middle" '
        f'font-size="16">{escape(axis_label)}</text>',
    ])
    for model, (_, color, _, _) in STYLES.items():
        if model not in series:
            continue
        ordered = sorted(series[model], key=lambda row: row["revision"])
        if len(ordered) > 1:
            points = " ".join(
                f'{x(row["revision"]):.2f},{y(row[key]):.2f}' for row in ordered
            )
            svg.append(
                f'<polyline data-model="{escape(model)}" points="{points}" '
                f'fill="none" stroke="{color}" stroke-width="2" stroke-opacity="0.8"/>'
            )
    for row in records:
        label, color, shape, size = STYLES[row["model"]]
        svg.extend([
            f'<g data-arm="{escape(row["arm"])}" data-revision="{row["revision"]}" '
            f'data-passes="{row["passes"]}" data-trials="{row["trials"]}" '
            f'data-metric="{key}" data-value="{row[key]}" '
            f'data-costed-trials="{row["costed_trials"]}" '
            f'transform="translate({x(row["revision"]):.2f} {y(row[key]):.2f})">',
            f'<title>{escape(label)} | CLI 1.0.{row["revision"]} | '
            f'{escape(axis_label)}: {escape(format_value(row[key]))} | '
            f'{row["passes"]}/{row["trials"]} passing trials | '
            f'Cost coverage: {row["costed_trials"]}/{row["trials"]}</title>',
            marker(shape, size, color),
            '</g>',
        ])
    for index, model in enumerate(model for model in STYLES if model in series):
        label, color, shape, size = STYLES[model]
        left, top = 105 + (index % 3) * 330, 575 + (index // 3) * 26
        svg.extend([
            f'<g transform="translate({left} {top})">{marker(shape, size, color)}</g>',
            f'<text x="{left + 17}" y="{top + 5}" font-size="14">{escape(label)}</text>',
        ])
    svg.extend([
        '<text x="95" y="696" font-size="13">'
        'Lines connect observations, not estimates for missing revisions.</text>',
        '<text x="95" y="717" font-size="13">'
        'Small samples and changing host/preparation settings limit causal comparisons.</text>',
        '</g></svg>',
    ])
    output.write_text("\n".join(svg) + "\n")
    print(f"Saved {output}: {len(records)} runs across {len(series)} models.")


def main():
    records = [summarize(path) for path in sorted(DATA.glob("*.jsonl"))]
    records = [
        row for row in records if row["model"] in STYLES and row["revision"] >= 95
    ]
    if not records:
        raise ValueError("No enterprise JSONL exports match the selected chart models")
    identities = {(row["model"], row["revision"]) for row in records}
    if len(identities) != len(records):
        raise ValueError("Multiple arms share a model and CLI revision")
    missing_cost = [row["arm"] for row in records if row["cost_per_trial"] is None]
    if missing_cost:
        raise ValueError(f"Cannot plot arms without retained cost evidence: {missing_cost}")
    upper = records[0]["trials"]
    lower = min(min(row["passes"] for row in records), upper - 1)
    cost_step = Decimal("0.20")
    cost_steps = max(1, ceil(max(row["cost_per_trial"] for row in records) / cost_step))
    duration_upper = max(60, ceil(max(row["agent_p90_seconds"] for row in records) / 60) * 60)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    plot(
        records, OUTPUT, "passes",
        "Enterprise passing trials by Copilot CLI revision", "Passing trials (out of 56)",
        list(range(lower, upper + 1)), str, str,
    )
    plot(
        records, OUTPUT.with_name("cost-per-trial-by-cli-revision.svg"), "cost_per_trial",
        "Enterprise cost per test by Copilot CLI revision", "USD per costed trial",
        [cost_step * step for step in range(cost_steps + 1)],
        lambda value: f"${value:.2f}", lambda value: f"${value:.4f}",
    )
    plot(
        records, OUTPUT.with_name("agent-duration-by-cli-revision.svg"), "agent_p90_seconds",
        "Enterprise p90 agent duration by Copilot CLI revision", "p90 agent time (m:ss)",
        list(range(0, duration_upper + 1, 60)), format_duration, format_duration,
    )


if __name__ == "__main__":
    main()
