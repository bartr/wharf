#!/usr/bin/env python3
"""Generate per-task summaries for reference baseline TSV exports."""

from __future__ import annotations

import csv
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
REFERENCE = REPO / "reference"


def difficulty(pass_rate: float) -> str:
    if pass_rate == 0:
        return "unsolved"
    if pass_rate < 0.5:
        return "hard"
    if pass_rate == 0.5:
        return "mixed"
    if pass_rate < 1:
        return "reliable"
    return "solved"


def summarize(path: Path) -> Path:
    grouped: dict[str, dict] = {}
    with path.open(encoding="utf-8", newline="") as fh:
        for row in csv.DictReader(fh, delimiter="\t"):
            task = row["task_name"]
            entry = grouped.setdefault(task, {
                "language": row["language"],
                "passes": 0,
                "costs": [],
            })
            entry["passes"] += row["outcome"] == "pass"
            entry["costs"].append(float(row["cost_usd"]))

    rows = []
    for task, entry in grouped.items():
        n_trials = len(entry["costs"])
        pass_rate = entry["passes"] / n_trials
        mean_usd = sum(entry["costs"]) / n_trials
        rows.append([
            task,
            entry["language"],
            n_trials,
            entry["passes"],
            round(pass_rate, 3),
            difficulty(pass_rate),
            round(mean_usd, 3),
            round(mean_usd * 4, 2),
        ])

    rows.sort(key=lambda row: (row[1], row[0]))
    output = path.with_name(path.stem + "_summary.csv")
    with output.open("w", encoding="utf-8", newline="") as fh:
        writer = csv.writer(fh)
        writer.writerow([
            "task", "language", "n_trials", "n_pass", "pass_rate",
            "difficulty", "mean_usd", "cost_4trials_usd",
        ])
        writer.writerows(rows)
    print(f"{output.relative_to(REPO)}: {len(rows)} tasks")
    return output


def main() -> int:
    paths = [REFERENCE / name for name in sys.argv[1:]]
    if not paths:
        paths = sorted(REFERENCE.glob("deepswe_v1_1_*.tsv"))
    for path in paths:
        if not path.is_file():
            raise SystemExit(f"reference TSV not found: {path}")
        summarize(path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
