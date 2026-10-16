#!/usr/bin/env python3
"""Plot non-HVE DeepSWE exports; requires matplotlib."""

import csv
import json
from collections import Counter
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.text import Text
from matplotlib.ticker import PercentFormatter, StrMethodFormatter


ROOT = Path(__file__).resolve().parent.parent
OUTPUT = ROOT / "docs" / "pass-at-1-vs-cost"
K5_ARMS = {
    "copilot-auto-max-1.0.77",
    "copilot-gpt-5.6-terra-max-1.0.77",
    "muse-spark-1.2-contributor-xhigh-0.1.0",
}


def summarize(path):
    rows = [json.loads(line) for line in path.read_text().splitlines() if line.strip()]
    counts = Counter(row["task_id"] for row in rows)
    k = 5 if path.stem in K5_ARMS else 4
    if len(counts) != 113 or max(counts.values()) > k:
        raise ValueError(f"{path}: expected the canonical 113-task K={k} arm")
    if len({(row["job"], row["trial"]) for row in rows}) != len(rows):
        raise ValueError(f"{path}: duplicate logical-trial identity")
    if any(row["arm"] != path.stem for row in rows):
        raise ValueError(f"{path}: arm identity mismatch")
    costs = [row["cost_usd"] for row in rows if row["cost_usd"] is not None]
    passed = sum(row["reward"] is not None and row["reward"] > 0 for row in rows)
    return {
        "arm": path.stem,
        "planned_trials": 113 * k,
        "retained_trials": len(rows),
        "passing_trials": passed,
        "pass_at_1_pct": 100 * passed / (113 * k),
        "known_cost_usd": sum(costs) if costs else None,
        "costed_trials": len(costs),
        "cost_coverage_pct": 100 * len(costs) / len(rows),
        "cost_per_costed_trial_usd": sum(costs) / len(costs) if costs else None,
    }


def label(arm):
    name = arm.removeprefix("copilot-").removeprefix("cc-")
    name = name.replace("-max-", " / ").replace("-xhigh-", " xhigh / ")
    name = name.replace("-high-", " high / ").replace("-default-", " / ")
    prefix = "Claude Code " if arm.startswith("cc-") else ""
    if "hydrafusion" in arm:
        name += " All-1" if not arm.endswith("-all-2") else ""
        name = name.replace("-all-2", " All-2")
    return prefix + name


def main():
    records = [
        summarize(path)
        for path in sorted((ROOT / "data").glob("*.jsonl"))
        if "-hve-" not in path.stem
    ]
    plotted = sorted(
        (row for row in records if row["costed_trials"]),
        key=lambda row: (row["cost_per_costed_trial_usd"], row["arm"]),
    )
    missing = [row for row in records if not row["costed_trials"]]
    if not plotted:
        raise ValueError("No non-HVE arms have retained cost evidence")

    plt.rcParams.update({"font.size": 11, "svg.fonttype": "none"})
    fig, (ax, table_ax) = plt.subplots(
        1, 2, figsize=(20, 10), gridspec_kw={"width_ratios": [1.15, 1]}
    )
    fig.subplots_adjust(left=0.055, right=0.98, bottom=0.19, top=0.87, wspace=0.10)
    fig.suptitle("DeepSWE non-HVE runs: PASS@1 vs. average cost", fontsize=22, y=0.97)
    fig.text(
        0.5, 0.925,
        "One point per retained run | Lower cost and higher PASS@1 are better",
        ha="center", fontsize=13,
    )
    ax.set_xlabel("Average cost per task attempt (USD; costed trials only)", labelpad=12)
    ax.set_ylabel("PASS@1 (passing / planned logical trials)", labelpad=12)
    ax.set_xlim(0, max(row["cost_per_costed_trial_usd"] for row in plotted) * 1.10)
    ax.set_ylim(0, 100)
    ax.xaxis.set_major_formatter(StrMethodFormatter("${x:.0f}"))
    ax.yaxis.set_major_formatter(PercentFormatter())
    ax.grid(alpha=0.2)
    ax.set_axisbelow(True)
    cells = []
    positions = []
    for number, row in enumerate(plotted, 1):
        hydra = "hydrafusion" in row["arm"]
        latest = row["arm"] == "copilot-hydrafusion-default-1.0.98"
        color = "#d55e00" if hydra else "#0072b2"
        x, y = row["cost_per_costed_trial_usd"], row["pass_at_1_pct"]
        ax.scatter(x, y, color=color, s=150 if latest else 65,
                   marker="*" if latest else "o", zorder=3)
        positions.append((number, x, y, color))
        cells.append([
            str(number), label(row["arm"]), f"{y:.2f}%", f"${x:.4f}",
            f'{row["costed_trials"]}/{row["retained_trials"]}',
        ])

    fig.canvas.draw()
    renderer = fig.canvas.get_renderer()
    occupied = []
    for number, x, y, color in positions:
        placed = False
        for distance in (12, 24, 36, 48):
            for dx, dy in ((1, 1), (1, -1), (-1, 1), (-1, -1), (0, 1), (0, -1)):
                annotation = ax.annotate(
                    str(number), (x, y), xytext=(dx * distance, dy * distance),
                    textcoords="offset points", ha="center", va="center",
                    color=color, weight="bold",
                    arrowprops={"arrowstyle": "-", "color": color, "lw": 0.6},
                )
                annotation.update_positions(renderer)
                box = Text.get_window_extent(annotation, renderer).expanded(1.1, 1.1)
                if not any(box.overlaps(other) for other in occupied):
                    occupied.append(box)
                    placed = True
                    break
                annotation.remove()
            if placed:
                break
        if not placed:
            raise ValueError(f"Could not place label for point {number}")

    table_ax.axis("off")
    table = table_ax.table(
        cellText=cells,
        colLabels=["#", "Run", "PASS@1", "$ / attempt", "Cost coverage"],
        colWidths=[0.05, 0.49, 0.13, 0.15, 0.18],
        cellLoc="left", colLoc="left", bbox=[0, 0.05, 1, 0.90],
    )
    table.auto_set_font_size(False)
    table.set_fontsize(10)
    for (row, col), cell in table.get_celld().items():
        cell.set_edgecolor("#dddddd")
        if row == 0:
            cell.set_facecolor("#e8eef5")
            cell.set_text_props(weight="bold")
        elif "hydrafusion" in plotted[row - 1]["arm"]:
            cell.set_facecolor("#fff0e5")
        elif row % 2 == 0:
            cell.set_facecolor("#f6f7f9")

    notes = [
        "PASS@1 uses K x 113 planned trials, including missing/error/cancelled attempts.",
        "X = sum(known USD cost) / costed retained trials. Missing costs are not imputed.",
        "HydraFusion is orange; the star marks 1.0.98. Historical max filenames retain their legacy labels.",
    ]
    notes.extend(
        f'Not plotted (no per-trial cost): {label(row["arm"])}; '
        f'PASS@1 {row["passing_trials"]}/{row["planned_trials"]} '
        f'({row["pass_at_1_pct"]:.2f}%).'
        for row in missing
    )
    fig.text(0.055, 0.025, "\n".join(notes), fontsize=11, linespacing=1.6)
    for extension in ("svg", "png"):
        fig.savefig(OUTPUT.with_suffix(f".{extension}"), dpi=160, facecolor="white")
    plt.close(fig)
    with OUTPUT.with_suffix(".csv").open("w", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(records[0]))
        writer.writeheader()
        writer.writerows(records)
    print(f"Saved {OUTPUT}.svg/.png/.csv: {len(plotted)} plotted, {len(missing)} without cost.")


if __name__ == "__main__":
    main()
