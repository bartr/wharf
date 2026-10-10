using System.Text.Json;
using Wharf.Agent;
using Wharf.Reporting;
using Wharf.State;

namespace Wharf.Tests;

public sealed class AnalyticsExporterTests
{
    [Fact]
    public void ExportsOneCanonicalRowPerLogicalTrialFromNativeEvidence()
    {
        string root = TestDirectory();
        try
        {
            string run = Path.Combine(root, "all-3");
            string trial = Path.Combine(run, "task-a__abcdef12__attempt-2");
            Directory.CreateDirectory(Path.Combine(trial, "agent"));
            Directory.CreateDirectory(Path.Combine(
                trial, "trial-retries", "retry-1", "agent"));
            Directory.CreateDirectory(Path.Combine(trial, "artifacts"));
            Directory.CreateDirectory(Path.Combine(trial, "verifier"));
            File.WriteAllText(
                Path.Combine(trial, "result.json"),
                """
                {
                  "status":"passed",
                  "taskId":"task-a",
                  "attempt":2,
                  "language":"go",
                  "taskChecksum":"full-checksum",
                  "totalCost":25
                }
                """);
            WriteAgentEvidence(
                Path.Combine(trial, "trial-retries", "retry-1", "agent", "result.json"),
                1500, 2, 10, 20, 3);
            WriteAgentEvidence(
                Path.Combine(trial, "agent", "result.json"),
                2500, 4, 30, 40, 7);
            File.WriteAllText(Path.Combine(trial, "artifacts", "model.patch"), "diff");
            File.WriteAllText(
                Path.Combine(trial, "verifier", "reward.json"),
                """
                {"reward":1,"f2p_passed":8,"f2p_total":10,"p2p_passed":5,"p2p_total":5}
                """);
            File.WriteAllLines(
                Path.Combine(run, "run.jsonl"),
                [
                    Stage("2026-10-10T00:00:01Z", "task-a", 2),
                    Stage("2026-10-10T00:00:10Z", "task-a", 2)
                ]);

            string output = Path.Combine(root, "data", "arm-a.jsonl");
            IReadOnlyList<AnalyticsTrial> rows = new AnalyticsExporter().Export(
                run,
                output,
                new AnalyticsExportOptions(
                    "arm-a", "copilot-cli", "1.0.95", "hydrafusion", "max", true));

            AnalyticsTrial row = Assert.Single(rows);
            Assert.Equal("task-a__abcdef12__attempt-2", row.Trial);
            Assert.Equal("all-3", row.Job);
            Assert.Equal(1, row.Reward);
            Assert.Equal(8, row.FunctionalTestsPassed);
            Assert.Equal(5, row.RegressionTestsPassed);
            Assert.Equal(25, row.Credits);
            Assert.Equal(0.25m, row.CostUsd);
            Assert.Equal(4, row.AgentSeconds);
            Assert.Equal(9, row.TrialSeconds);
            Assert.Equal(4, row.PatchBytes);
            Assert.Equal(40, row.InputTokens);
            Assert.Equal(60, row.OutputTokens);
            Assert.Equal(10, row.CacheTokens);
            Assert.Equal(6, row.AgentSteps);
            Assert.Equal("full-checksum", row.TaskChecksum);
            Assert.Single(File.ReadLines(output));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void OrdersLogicalTrialsDeterministically()
    {
        string root = TestDirectory();
        try
        {
            string run = Path.Combine(root, "run");
            Directory.CreateDirectory(run);
            WriteMinimalTrial(run, "task-z__2", "task-z");
            WriteMinimalTrial(run, "task-a__1", "task-a");

            IReadOnlyList<AnalyticsTrial> rows = new AnalyticsExporter().Export(
                run,
                Path.Combine(root, "rows.jsonl"),
                new AnalyticsExportOptions(
                    "arm", "copilot-cli", null, "model", "max", false));

            Assert.Equal(["task-a__1", "task-z__2"], rows.Select(row => row.Trial));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PreservesExplicitlyUnknownTerminalCost()
    {
        string root = TestDirectory();
        try
        {
            string run = Path.Combine(root, "run");
            string trial = Path.Combine(run, "task-a__1");
            Directory.CreateDirectory(Path.Combine(trial, "agent"));
            File.WriteAllText(
                Path.Combine(trial, "result.json"),
                """{"status":"agent-failed","taskId":"task-a","attempt":1,"totalCost":null}""");
            WriteAgentEvidence(
                Path.Combine(trial, "agent", "result.json"),
                1000, 1, 10, 20, 3);

            IReadOnlyList<AnalyticsTrial> rows = new RunEvidenceReader().Read(
                run,
                new AnalyticsExportOptions(
                    "arm", "copilot-cli", null, "model", "max", false));

            AnalyticsTrial row = Assert.Single(rows);
            Assert.Null(row.Credits);
            Assert.Null(row.CostUsd);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteMinimalTrial(string run, string name, string taskId)
    {
        string trial = Path.Combine(run, name);
        Directory.CreateDirectory(trial);
        File.WriteAllText(
            Path.Combine(trial, "result.json"),
            $$"""{"status":"failed","taskId":"{{taskId}}","attempt":1}""");
    }

    private static void WriteAgentEvidence(
        string path,
        long durationMilliseconds,
        int steps,
        long input,
        long output,
        long cache)
    {
        var usage = new AgentUsage(
            input, output, cache, 0, 0, 1, 0, 10,
            new Dictionary<string, AgentModelUsage>());
        var summary = new AgentEvidenceSummary(
            AgentEvidenceSchema.Version,
            "task-a",
            "completed",
            0,
            durationMilliseconds,
            null,
            steps,
            1,
            10,
            "patch.diff",
            "stdout.log",
            "stderr.log",
            "trajectory.jsonl",
            usage);
        File.WriteAllText(path, JsonSerializer.Serialize(summary, StateJson.Options));
    }

    private static string Stage(string timestamp, string taskId, int attempt) =>
        $$"""{"timestamp":"{{timestamp}}","eventType":"stage","taskId":"{{taskId}}","logicalAttempt":{{attempt}}}""";

    private static string TestDirectory()
    {
        string path = Path.Combine(
            Environment.CurrentDirectory,
            "test-artifacts",
            $"analytics-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
