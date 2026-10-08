#!/usr/bin/env python3

import argparse
import json
import statistics
import tomllib
from datetime import datetime
from pathlib import Path


def parse_timestamp(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def read_durations(
    pier_run: Path,
) -> tuple[dict[str, list[float]], dict[str, list[float]]]:
    agent_durations: dict[str, list[float]] = {}
    verifier_durations: dict[str, list[float]] = {}
    for result_path in sorted(pier_run.glob("*/result.json")):
        with result_path.open(encoding="utf-8") as stream:
            result = json.load(stream)
        task_name = result.get("task_name")
        execution = result.get("agent_execution")
        verifier = result.get("verifier")
        if (
            not isinstance(task_name, str)
            or not isinstance(execution, dict)
            or not isinstance(verifier, dict)
        ):
            continue
        task_id = task_name.rsplit("/", 1)[-1]
        for stage, values, target in [
            ("agent", execution, agent_durations),
            ("verifier", verifier, verifier_durations),
        ]:
            started_at = values.get("started_at")
            finished_at = values.get("finished_at")
            if not isinstance(started_at, str) or not isinstance(finished_at, str):
                break
            seconds = (
                parse_timestamp(finished_at) - parse_timestamp(started_at)
            ).total_seconds()
            if seconds <= 0:
                raise ValueError(f"non-positive {stage} duration in {result_path}")
            target.setdefault(task_id, []).append(seconds)
    return agent_durations, verifier_durations


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
        description="Import median Pier agent and verifier durations into Wharf task metadata."
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

    agent_durations, verifier_durations = read_durations(pier_run)
    languages = task_languages(task_root)
    expected = sorted(languages)
    missing = [
        task_id
        for task_id in expected
        if task_id not in agent_durations or task_id not in verifier_durations
    ]
    unknown = sorted(
        (set(agent_durations) | set(verifier_durations)) - set(expected)
    )
    if missing:
        raise ValueError(f"Pier run is missing task durations: {', '.join(missing)}")
    if unknown:
        raise ValueError(f"Pier run contains unknown tasks: {', '.join(unknown)}")

    agent_medians = {
        task_id: round(statistics.median(agent_durations[task_id]), 3)
        for task_id in expected
    }
    verifier_medians = {
        task_id: round(statistics.median(verifier_durations[task_id]), 3)
        for task_id in expected
    }
    combined_medians = {
        task_id: agent_medians[task_id] + verifier_medians[task_id]
        for task_id in expected
    }
    scheduling = schedule_durations(
        combined_medians,
        languages,
        arguments.spaced_language,
        arguments.first_position,
        arguments.spacing,
    )
    estimates = {
        task_id: {
            "language": languages[task_id],
            "medianAgentDurationSeconds": agent_medians[task_id],
            "medianVerifierDurationSeconds": verifier_medians[task_id],
            "schedulingDurationSeconds": scheduling[task_id],
        }
        for task_id in expected
    }
    document = {
        "schemaVersion": "1.2",
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
        f"{sum(len(values) for values in agent_durations.values())} Pier attempts to {output}"
    )


if __name__ == "__main__":
    main()
