import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


SPEC = importlib.util.spec_from_file_location(
    "codex_costs", Path(__file__).resolve().parents[1]
    / "scripts/estimate-wharf-swe-codex-costs.py")
COSTS = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(COSTS)


def summary(input_tokens=1000, cached=800, output=100):
    return {"usage": {
        "inputTokens": input_tokens, "cacheReadTokens": cached,
        "cacheWriteTokens": 0, "outputTokens": output, "reasoningTokens": 25,
    }}


def row(**overrides):
    return {
        "arm": "codex-example", "job": "all-1", "trial": "task__attempt-1",
        "agent": "codex", "model": "gpt-6.1-sol", "cost_usd": None,
        "n_input_tokens": 1000, "n_cache_tokens": 800, "n_output_tokens": 100,
        **overrides,
    }


class CodexCostEstimateTests(unittest.TestCase):
    def test_prices_uncached_cached_and_output_without_recharging_reasoning(self):
        source = row()
        result = COSTS.estimate(source, [summary()])
        self.assertAlmostEqual(result["estimated_cost_usd"], 0.00148)
        self.assertIsNone(result["cost_usd"])
        self.assertTrue(result["cost_estimate"]["complete_usage"])
        self.assertEqual(source, {key: result[key] for key in source})

    def test_whole_trial_and_infrastructure_retries_are_summed_once(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = row(n_input_tokens=3000, n_cache_tokens=2400, n_output_tokens=300)
            trial = root / source["arm"] / source["job"] / source["trial"]
            for relative in (
                "agent", "agent/infrastructure-retries/retry-1",
                "trial-retries/retry-1/agent",
            ):
                agent = trial / relative
                agent.mkdir(parents=True, exist_ok=True)
                (agent / "result.json").write_text(json.dumps(summary()))
            export = root / f"{source['arm']}.jsonl"
            export.write_text(json.dumps(source) + "\n")
            result = json.loads(COSTS.enrich(export, root))
            self.assertAlmostEqual(result["estimated_cost_usd"], 0.00444)
            self.assertEqual(result["cost_estimate"]["agent_executions"], 3)

    def test_missing_retry_usage_does_not_become_a_partial_trial_estimate(self):
        result = COSTS.estimate(row(), [summary(), {"usage": None}])
        self.assertIsNone(result["estimated_cost_usd"])
        self.assertFalse(result["cost_estimate"]["complete_usage"])
        result = COSTS.estimate(row(
            n_input_tokens=None, n_cache_tokens=None, n_output_tokens=None
        ), [{"usage": None}])
        self.assertIsNone(result["estimated_cost_usd"])

    def test_invalid_or_mismatched_accounting_fails_explicitly(self):
        for evidence in (
            summary(cached=1001), summary(input_tokens=-1), summary(input_tokens=True),
            summary(output=101), {"usage": {**summary()["usage"], "cacheWriteTokens": 1}},
        ):
            with self.subTest(evidence=evidence), self.assertRaises(ValueError):
                COSTS.estimate(row(), [evidence])
        with self.assertRaises(ValueError):
            COSTS.estimate(row(), [])
        with self.assertRaises(ValueError):
            COSTS.component("../outside")


if __name__ == "__main__":
    unittest.main()
