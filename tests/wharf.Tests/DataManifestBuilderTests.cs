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
                Row("arm-a", "trial-1", "task-1", 1, 0.125m) + Environment.NewLine +
                Row("arm-a", "trial-2", "task-2", null, 0.126m) + Environment.NewLine);

            IReadOnlyList<DataManifestEntry> entries =
                new DataManifestBuilder().Build(root);

            Assert.Equal(["arm-a", "arm-b"], entries.Select(entry => entry.Arm));
            DataManifestEntry arm = entries[0];
            Assert.Equal(2, arm.Trials);
            Assert.Equal(2, arm.Tasks);
            Assert.Equal(1, arm.Scored);
            Assert.Equal(2, arm.WithCost);
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

    private static string Row(
        string arm,
        string trial,
        string task,
        double? reward,
        decimal? cost)
    {
        string rewardJson = reward?.ToString(
            System.Globalization.CultureInfo.InvariantCulture) ?? "null";
        string costJson = cost?.ToString(
            System.Globalization.CultureInfo.InvariantCulture) ?? "null";
        return $$"""
            {"arm":"{{arm}}","job":"job","trial":"{{trial}}","task_id":"{{task}}","language":"go","agent":"copilot-cli","agent_version":"1","model":"model","effort":"max","subagents":false,"reward":{{rewardJson}},"f2p_passed":null,"f2p_total":null,"p2p_passed":null,"p2p_total":null,"cost_usd":{{costJson}},"credits":null,"premium_requests":null,"n_input_tokens":null,"n_cache_tokens":null,"n_output_tokens":null,"n_agent_steps":null,"peak_context_tokens":null,"agent_seconds":null,"trial_seconds":null,"patch_bytes":null,"exception_type":null,"task_checksum":null}
            """;
    }
}
