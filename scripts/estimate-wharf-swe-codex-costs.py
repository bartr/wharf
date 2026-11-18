#!/usr/bin/env python3
"""Add explicitly labeled API-equivalent estimates, never subscription charges."""

import json
import os
from decimal import Decimal
from pathlib import Path
import tempfile


ROOT = Path(__file__).resolve().parent.parent
PRICING_SOURCE = "https://developers.openai.com/api/docs/pricing.md"
PRICING_CHECKED = "2026-11-18"
RATES = {
    "gpt-6-astra": ("10", "1", "50"),
    "gpt-6.1-sol": ("2", "0.10", "10"),
    "gpt-6-luna": ("0.10", "0.01", "0.50"),
}


def token_count(usage, field):
    value = usage[field]
    if type(value) is not int or value < 0:
        raise ValueError(f"{field} must be a nonnegative integer")
    return value


def estimate(row, summaries):
    if row["agent"] != "codex":
        raise ValueError("Only Codex rows may receive these estimates")
    rates = tuple(Decimal(value) for value in RATES[row["model"]])
    usages = [summary["usage"] for summary in summaries]
    if not usages:
        raise ValueError(f"{row['trial']}: no retained agent execution evidence")
    total = Decimal(0)
    tokens = [0, 0, 0]
    for usage in usages:
        if usage is None:
            continue
        input_tokens = token_count(usage, "inputTokens")
        cached = token_count(usage, "cacheReadTokens")
        output = token_count(usage, "outputTokens")
        if cached > input_tokens or token_count(usage, "cacheWriteTokens") != 0:
            raise ValueError("Unsupported Codex cache accounting")
        total += (
            (input_tokens - cached) * rates[0]
            + cached * rates[1]
            + output * rates[2]
        ) / Decimal(1_000_000)
        tokens = [a + b for a, b in zip(tokens, (input_tokens, cached, output))]
    measured = [usage for usage in usages if usage is not None]
    expected = tokens if measured else [None, None, None]
    if expected != [row[field] for field in (
        "n_input_tokens", "n_cache_tokens", "n_output_tokens"
    )]:
        raise ValueError(f"{row['trial']}: exported tokens differ from retained evidence")
    complete = len(measured) == len(usages)
    return {
        **row,
        "estimated_cost_usd": float(total) if complete else None,
        "cost_estimate": {
            "basis": "api_equivalent_not_subscription_charge",
            "pricing_source": PRICING_SOURCE,
            "pricing_checked_on": PRICING_CHECKED,
            "tier": "Standard",
            "context_assumption": "At most 272000 input tokens per request",
            "rates_usd_per_million_tokens": dict(zip(
                ("input", "cached_input", "output"), map(float, rates)
            )),
            "formula": "((input - cached_input) * input_rate + cached_input * "
                       "cached_rate + output * output_rate) / 1000000",
            "agent_executions": len(usages),
            "executions_with_usage": len(measured),
            "complete_usage": complete,
        },
    }


def component(value):
    if not value or value in (".", "..") or Path(value).name != value:
        raise ValueError(f"Expected a single path component: {value!r}")
    return value


def enrich(path, jobs):
    rows = [json.loads(line) for line in path.read_text().splitlines() if line.strip()]
    enriched = []
    for row in rows:
        if row["arm"] != path.stem:
            raise ValueError(f"{path}: arm identity mismatch")
        trial = jobs / component(row["arm"]) / component(row["job"]) / component(row["trial"])
        agents = [trial / "agent"]
        agents.extend(sorted((trial / "trial-retries").glob("*/agent")))
        summaries = []
        for agent in agents:
            paths = [agent / "result.json"]
            paths.extend(sorted((agent / "infrastructure-retries").glob("*/result.json")))
            summaries.extend(json.loads(item.read_text()) for item in paths)
        enriched.append(estimate(row, summaries))
    return "".join(json.dumps(row, separators=(",", ":"), allow_nan=False) + "\n"
                   for row in enriched)


def main():
    data = ROOT / "wharf-swe" / "data"
    exports = sorted(data.glob("codex-*.jsonl"))
    if not exports:
        raise ValueError("No Codex exports found")
    outputs = [(path, enrich(path, ROOT / "wharf-swe" / "jobs")) for path in exports]
    for path, content in outputs:
        temporary = None
        try:
            with tempfile.NamedTemporaryFile(mode="w", dir=path.parent, delete=False) as stream:
                temporary = Path(stream.name)
                stream.write(content)
            os.replace(temporary, path)
        finally:
            if temporary is not None:
                temporary.unlink(missing_ok=True)
        print(f"Added API-equivalent estimates: {path.name}")


if __name__ == "__main__":
    main()
