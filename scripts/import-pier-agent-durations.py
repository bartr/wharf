#!/usr/bin/env python3

import argparse
import json
import statistics
import tomllib
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


def task_languages(task_root: Path) -> dict[str, str]:
    tasks: dict[str, str] = {}
    for directory in sorted(task_root.iterdir()):
        manifest = directory / "task.toml"
        if not directory.is_dir() or not manifest.is_file():
            continue
        with manifest.open("rb") as stream:
            metadata = tomllib.load(stream)["metadata"]
        task_id = metadata["task_id"]
        if task_id != directory.name:
            raise ValueError(
                f"task ID '{task_id}' does not match directory '{directory.name}'"
            )
        tasks[task_id] = metadata["language"]
    return tasks


def schedule_durations(
    medians: dict[str, float],
    languages: dict[str, str],
    spaced_language: str,
    first_position: int,
    spacing: int,
) -> dict[str, float]:
    spaced = sorted(
        (task_id for task_id, language in languages.items() if language == spaced_language),
        key=lambda task_id: (-medians[task_id], task_id),
    )
    regular = sorted(
        (task_id for task_id, language in languages.items() if language != spaced_language),
        key=lambda task_id: (-medians[task_id], task_id),
    )
    target_indices = {
        first_position - 1 + index * spacing
        for index in range(len(spaced))
    }
    if target_indices and max(target_indices) >= len(languages):
        raise ValueError(
            f"cannot place {len(spaced)} {spaced_language} tasks every {spacing} admissions"
        )

    ordered: list[str] = []
    spaced_index = 0
    regular_index = 0
    for index in range(len(languages)):
        if index in target_indices:
            ordered.append(spaced[spaced_index])
            spaced_index += 1
        else:
            ordered.append(regular[regular_index])
            regular_index += 1

    reference = sorted(medians.values(), reverse=True)
    count = len(ordered)
    return {
        task_id: round(reference[index] + (count - index) / 1_000_000, 6)
        for index, task_id in enumerate(ordered)
    }


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
    parser.add_argument("--spaced-language", default="rust")
    parser.add_argument("--first-position", type=int, default=1)
    parser.add_argument("--spacing", type=int, default=16)
    arguments = parser.parse_args()

    pier_run = arguments.pier_run.resolve()
    task_root = arguments.task_root.resolve()
    if not pier_run.is_dir():
        raise ValueError(f"Pier run directory does not exist: {pier_run}")
    if not task_root.is_dir():
        raise ValueError(f"task root does not exist: {task_root}")

    durations = read_durations(pier_run)
    languages = task_languages(task_root)
    expected = sorted(languages)
    missing = [task_id for task_id in expected if task_id not in durations]
    unknown = sorted(set(durations) - set(expected))
    if missing:
        raise ValueError(f"Pier run is missing task durations: {', '.join(missing)}")
    if unknown:
        raise ValueError(f"Pier run contains unknown tasks: {', '.join(unknown)}")

    medians = {
        task_id: round(statistics.median(durations[task_id]), 3)
        for task_id in expected
    }
    scheduling = schedule_durations(
        medians,
        languages,
        arguments.spaced_language,
        arguments.first_position,
        arguments.spacing,
    )
    estimates = {
        task_id: {
            "language": languages[task_id],
            "medianAgentDurationSeconds": medians[task_id],
            "schedulingAgentDurationSeconds": scheduling[task_id],
        }
        for task_id in expected
    }
    document = {
        "schemaVersion": "1.1",
        "source": arguments.source,
        "aggregation": "median",
        "unit": "seconds",
        "scheduling": {
            "strategy": "language-stride",
            "language": arguments.spaced_language,
            "firstPosition": arguments.first_position,
            "spacing": arguments.spacing,
        },
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
