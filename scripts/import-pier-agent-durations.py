#!/usr/bin/env python3

import argparse
import json
import statistics
from datetime import datetime
from pathlib import Path


def parse_timestamp(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def read_durations(pier_run: Path) -> dict[str, list[float]]:
    durations: dict[str, list[float]] = {}
    for result_path in sorted(pier_run.glob("*/result.json")):
        with result_path.open(encoding="utf-8") as stream:
            result = json.load(stream)
        task_name = result.get("task_name")
        execution = result.get("agent_execution")
        if not isinstance(task_name, str) or not isinstance(execution, dict):
            continue
        started_at = execution.get("started_at")
        finished_at = execution.get("finished_at")
        if not isinstance(started_at, str) or not isinstance(finished_at, str):
            continue
        seconds = (parse_timestamp(finished_at) - parse_timestamp(started_at)).total_seconds()
        if seconds <= 0:
            raise ValueError(f"non-positive agent duration in {result_path}")
        task_id = task_name.rsplit("/", 1)[-1]
        durations.setdefault(task_id, []).append(seconds)
    return durations


def task_ids(task_root: Path) -> list[str]:
    return sorted(
        directory.name
        for directory in task_root.iterdir()
        if directory.is_dir() and (directory / "task.toml").is_file()
    )


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Import median Pier agent durations into Wharf task metadata."
    )
    parser.add_argument("pier_run", type=Path)
    parser.add_argument("task_root", type=Path)
    parser.add_argument(
        "--source",
        required=True,
        help="Stable provenance label written to the estimate document.",
    )
    arguments = parser.parse_args()

    pier_run = arguments.pier_run.resolve()
    task_root = arguments.task_root.resolve()
    if not pier_run.is_dir():
        raise ValueError(f"Pier run directory does not exist: {pier_run}")
    if not task_root.is_dir():
        raise ValueError(f"task root does not exist: {task_root}")

    durations = read_durations(pier_run)
    expected = task_ids(task_root)
    missing = [task_id for task_id in expected if task_id not in durations]
    unknown = sorted(set(durations) - set(expected))
    if missing:
        raise ValueError(f"Pier run is missing task durations: {', '.join(missing)}")
    if unknown:
        raise ValueError(f"Pier run contains unknown tasks: {', '.join(unknown)}")

    estimates = {
        task_id: round(statistics.median(durations[task_id]), 3)
        for task_id in expected
    }
    document = {
        "schemaVersion": "1.0",
        "source": arguments.source,
        "aggregation": "median",
        "unit": "seconds",
        "tasks": estimates,
    }
    output = task_root / "agent-duration-estimates.json"
    output.write_text(
        json.dumps(document, indent=2, sort_keys=False) + "\n",
        encoding="utf-8",
    )
    print(
        f"wrote {len(estimates)} task estimates from "
        f"{sum(len(values) for values in durations.values())} Pier attempts to {output}"
    )


if __name__ == "__main__":
    main()
