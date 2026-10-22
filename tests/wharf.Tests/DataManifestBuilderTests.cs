using Wharf.Reporting;

namespace Wharf.Tests;

public sealed class DataManifestBuilderTests
{
    [Fact]
    public void BuildsManifestOnlyFromPortableJsonLines()
    {
        string root = Path.Combine(
            Environment.CurrentDirectory,
            "test-artifacts",
            $"manifest-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "jobs"));
            File.WriteAllText(
                Path.Combine(root, "b.jsonl"),
                Row("arm-b", "trial-2", "task-1", 0, null) + Environment.NewLine);
            File.WriteAllText(
                Path.Combine(root, "a.jsonl"),
                Row("arm-a", "trial-1", "task-1", 1, 0.125m, 1, 1, 10) +
                Environment.NewLine +
                Row("arm-a", "trial-2", "task-2", null, 0.126m, 0, 1, 20) +
                Environment.NewLine);

            IReadOnlyList<DataManifestEntry> entries =
                new DataManifestBuilder().Build(root);

            Assert.Equal(["arm-a", "arm-b"], entries.Select(entry => entry.Arm));
            DataManifestEntry arm = entries[0];
            Assert.Equal(2, arm.Trials);
            Assert.Equal(2, arm.Tasks);
            Assert.Equal("copilot-cli", arm.Cli);
            Assert.Equal("1", arm.CliVersion);
            Assert.Equal("max", arm.Effort);
            Assert.Equal("model", arm.Model);
            Assert.Equal(1, arm.Passes);
            Assert.Equal(1, arm.P2PErrors);
            Assert.Equal(0.4975m, arm.Score);
            Assert.Equal(1, arm.Scored);
            Assert.Equal(2, arm.WithCost);
            Assert.Equal(0.1255m, arm.CostPerTrialUsd);
            Assert.Equal(15d, arm.AverageAgentSeconds);
            Assert.Equal(0.25m, arm.TotalCostUsd);
            Assert.True(arm.BytesJsonl > 0);
            Assert.True(File.Exists(Path.Combine(root, "manifest.json")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void AcceptsFractionalAllocatedPremiumRequests()
    {
        string root = Path.Combine(
            Environment.CurrentDirectory,
            "test-artifacts",
            $"manifest-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            string row = Row("arm-a", "trial-1", "task-a", 1, 0.25m)
                .Replace(
                    "\"premium_requests\":null",
                    "\"premium_requests\":0.33",
                    StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(root, "arm-a.jsonl"), row + Environment.NewLine);

            IReadOnlyList<DataManifestEntry> entries =
                new DataManifestBuilder().Build(root);

            Assert.Single(entries);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(0, 0, 0, null)]
    [InlineData(1, 1, 0, "1")]
    [InlineData(2, 1, 1, "0.4975")]
    [InlineData(1, 0, 1, "-0.0025")]
    [InlineData(4, 4, 2, "0.995")]
    public void ScoreUsesAllTrialsAndSubtractsQuarterPointPerRegression(
        int trials,
        int passes,
        int p2pErrors,
        string? expectedScore)
    {
        string root = Path.Combine(
            Environment.CurrentDirectory,
            "test-artifacts",
            $"manifest-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllLines(
                Path.Combine(root, "arm.jsonl"),
                Enumerable.Range(0, trials).Select(index => Row(
                    "arm", $"trial-{index}", "task",
                    index < passes ? 1 : null, null,
                    index < p2pErrors ? 0 : 3, 3)));

            DataManifestEntry entry = Assert.Single(new DataManifestBuilder().Build(root));
            decimal? expected = expectedScore is null
                ? null
                : decimal.Parse(expectedScore, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(expected, entry.Score);
            using var manifest = System.Text.Json.JsonDocument.Parse(
                File.ReadAllText(Path.Combine(root, "manifest.json")));
            System.Text.Json.JsonElement score = manifest.RootElement[0].GetProperty("score");
            if (expected.HasValue)
            {
                Assert.Equal(expected.Value, score.GetDecimal());
            }
            else
            {
                Assert.Equal(System.Text.Json.JsonValueKind.Null, score.ValueKind);
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static string Row(
        string arm,
        string trial,
        string task,
        double? reward,
        decimal? cost,
        int? p2pPassed = 1,
        int? p2pTotal = 1,
        double? agentSeconds = 10)
    {
        string rewardJson = reward?.ToString(
            System.Globalization.CultureInfo.InvariantCulture) ?? "null";
        string costJson = cost?.ToString(
            System.Globalization.CultureInfo.InvariantCulture) ?? "null";
        string p2pPassedJson = p2pPassed?.ToString(
            System.Globalization.CultureInfo.InvariantCulture) ?? "null";
        string p2pTotalJson = p2pTotal?.ToString(
            System.Globalization.CultureInfo.InvariantCulture) ?? "null";
        string agentSecondsJson = agentSeconds?.ToString(
            System.Globalization.CultureInfo.InvariantCulture) ?? "null";
        return $$"""
            {"arm":"{{arm}}","job":"job","trial":"{{trial}}","task_id":"{{task}}","language":"go","agent":"copilot-cli","agent_version":"1","model":"model","effort":"max","subagents":false,"reward":{{rewardJson}},"f2p_passed":null,"f2p_total":null,"p2p_passed":{{p2pPassedJson}},"p2p_total":{{p2pTotalJson}},"cost_usd":{{costJson}},"credits":null,"premium_requests":null,"n_input_tokens":null,"n_cache_tokens":null,"n_output_tokens":null,"n_agent_steps":null,"peak_context_tokens":null,"agent_seconds":{{agentSecondsJson}},"trial_seconds":null,"patch_bytes":null,"exception_type":null,"task_checksum":null}
            """;
    }
}
