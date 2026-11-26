using System.Text.Json;
using System.Text.Json.Nodes;
using Wharf.Configuration;
using Wharf.Recovery;
using Wharf.Reporting;
using Wharf.Run;
using Wharf.State;
using Wharf.Tasks;

namespace Wharf.Tests;

public sealed class AnalyticsExportDiscoveryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"wharf-analytics-discovery-{Guid.NewGuid():N}");
    private const string Arm = "copilot-test-model-max-1.0.95";

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void DiscoveryCombinesCompletedJobsAndSkipsIncompleteRuns()
    {
        string first = CreateRun();
        CreateRun(job: "all-2");
        string incomplete = CreateRun(job: "all-3", completed: false);
        CreateRun(model: "other-model");
        var messages = new List<string>();

        int trials = new AnalyticsExportDiscovery().Export(
            new AnalyticsDiscoveryOptions(Path.Combine(root, "jobs")), messages.Add);

        Assert.Equal(6, trials);
        AnalyticsTrial[] rows = Rows();
        Assert.Equal(4, rows.Length);
        Assert.Equal(["all-1", "all-1", "all-2", "all-2"], rows.Select(row => row.Job));
        Assert.Equal(2, rows.Select(row => row.Trial).Distinct().Count());
        Assert.All(rows, row =>
        {
            Assert.Equal("copilot-cli", row.Agent);
            Assert.Equal("test-model", row.Model);
            Assert.Equal("1.0.95", row.AgentVersion);
            Assert.Null(row.Subagents);
        });
        string checksum = RetainedRunMetadataStore.Read(first)!.Tasks[0].SourceChecksum;
        Assert.All(rows, row => Assert.Equal(checksum, row.TaskChecksum));
        Assert.False(Directory.Exists(Path.Combine(root, "absent-tasks")));
        Assert.Contains(messages, message => message.Contains(incomplete) && message.Contains("incomplete"));

        DataManifestEntry entry = new DataManifestBuilder().Build(Path.Combine(root, "data"))
            .Single(item => item.Arm == Arm);
        Assert.Equal(4, entry.Trials);
    }

    [Fact]
    public void ExistingExportsAreUntouchedUnlessRecomputeIsRequested()
    {
        CreateRun();
        string jobs = Path.Combine(root, "jobs");
        var exporter = new AnalyticsExportDiscovery();
        exporter.Export(new AnalyticsDiscoveryOptions(jobs), _ => { });
        string output = Path.Combine(root, "data", $"{Arm}.jsonl");
        File.WriteAllText(output, "retained bytes");
        CreateRun(job: "all-2");

        Assert.Equal(0, exporter.Export(new AnalyticsDiscoveryOptions(jobs), _ => { }));
        Assert.Equal("retained bytes", File.ReadAllText(output));
        Assert.Equal(4, exporter.Export(new AnalyticsDiscoveryOptions(jobs, Recompute: true), _ => { }));
        Assert.Equal(4, Rows().Length);
    }

    [Fact]
    public void DiscoveryDoesNotMixDifferentSuiteJobsTrees()
    {
        CreateRun();
        string otherRun = CreateRun(model: "other-model");
        string arm = Path.GetDirectoryName(otherRun)!;
        string otherJobs = Path.Combine(root, "other-suite", "jobs");
        Directory.CreateDirectory(otherJobs);
        Directory.Move(arm, Path.Combine(otherJobs, Path.GetFileName(arm)));
        AnalyticsEvidenceException error = Assert.Throws<AnalyticsEvidenceException>(() =>
            new AnalyticsExportDiscovery().Export(new AnalyticsDiscoveryOptions(root), _ => { }));
        Assert.Contains("multiple jobs trees", error.Message);
        Assert.False(Directory.Exists(Path.Combine(root, "data")));
    }

    [Fact]
    public void CompetingOutputCreationIsNotOverwritten()
    {
        CreateRun();
        string data = Path.Combine(root, "data");
        string output = Path.Combine(data, $"{Arm}.jsonl");
        int count = new AnalyticsExportDiscovery().Export(
            new AnalyticsDiscoveryOptions(Path.Combine(root, "jobs")), message =>
            {
                if (message.StartsWith("exporting ", StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(data);
                    File.WriteAllText(output, "another writer");
                }
            });
        Assert.Equal(0, count);
        Assert.Equal("another writer", File.ReadAllText(output));
        Assert.Empty(Directory.GetFiles(data, "*.tmp"));
    }

    [Theory]
    [InlineData("running")]
    [InlineData("quota-paused")]
    [InlineData("cancelled")]
    public void NoncompletedBatchIsSkippedEvenWithTerminalResults(string status)
    {
        string run = CreateRun();
        File.WriteAllText(Path.Combine(run, RunBatch.FileName), JsonSerializer.Serialize(
            new BatchState("1.0",
                [new BatchInvocation("batch-1", null, DateTimeOffset.UtcNow, status, [], [], [], [], Pending: 1)], []),
            StateJson.Options));
        var messages = new List<string>();
        Assert.Equal(0, new AnalyticsExportDiscovery().Export(new AnalyticsDiscoveryOptions(run), messages.Add));
        Assert.Contains(messages, message => message.Contains($"status={status}"));
        Assert.False(Directory.Exists(Path.Combine(root, "data")));
    }

    [Fact]
    public void LegacyCompletedRunUsesArmAndPlanLogWithoutTaskCheckout()
    {
        string run = CreateRun(native: false);
        Assert.Equal(2, new AnalyticsExportDiscovery().Export(new AnalyticsDiscoveryOptions(run), _ => { }));
        Assert.All(Rows(), row =>
        {
            Assert.Equal("test-model", row.Model);
            Assert.Equal("max", row.Effort);
            Assert.Null(row.TaskChecksum);
        });
    }

    [Fact]
    public void ActiveWriterIsSkippedWithoutWaitingOrChangingEvidence()
    {
        string run = CreateRun();
        using var writer = new FileStream(Path.Combine(run, ".writer.lock"),
            FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        var messages = new List<string>();
        Assert.Equal(0, new AnalyticsExportDiscovery().Export(new AnalyticsDiscoveryOptions(run), messages.Add));
        Assert.Contains(messages, message => message.Contains("run-writer-unavailable"));
        Assert.False(Directory.Exists(Path.Combine(root, "data")));
    }

    [Theory]
    [InlineData("broken-json")]
    [InlineData("wrong-attempt")]
    [InlineData("wrong-checksum")]
    [InlineData("running")]
    public void InvalidTerminalEvidenceMakesRunIneligible(string defect)
    {
        string run = CreateRun(k: 1);
        string result = Path.Combine(Directory.EnumerateDirectories(run).Single(), "result.json");
        string original = File.ReadAllText(result);
        File.WriteAllText(result, defect switch
        {
            "broken-json" => "{",
            "wrong-attempt" => original.Replace("\"attempt\":1", "\"attempt\":2", StringComparison.Ordinal),
            "wrong-checksum" => original.TrimEnd('}') + ",\"taskChecksum\":\"wrong\"}",
            _ => original.Replace("\"failed\"", "\"running\"", StringComparison.Ordinal)
        });
        Assert.Equal(0, new AnalyticsExportDiscovery().Export(new AnalyticsDiscoveryOptions(run), _ => { }));
        Assert.False(Directory.Exists(Path.Combine(root, "data")));
    }

    [Theory]
    [InlineData("model")]
    [InlineData("agent-version")]
    [InlineData("effort")]
    [InlineData("agent")]
    [InlineData("arm")]
    [InlineData("job")]
    public void ContradictoryLabelsFailBeforeCreatingOutput(string label)
    {
        string run = CreateRun();
        var options = new AnalyticsDiscoveryOptions(run);
        options = label switch
        {
            "model" => options with { Model = "wrong" },
            "agent-version" => options with { AgentVersion = "9.9.9" },
            "effort" => options with { Effort = "low" },
            "agent" => options with { Agent = "claude" },
            "job" => options with { Job = "wrong" },
            _ => options with { Arm = "wrong" }
        };
        AnalyticsEvidenceException error = Assert.Throws<AnalyticsEvidenceException>(() =>
            new AnalyticsExportDiscovery().Export(options, _ => { }));
        Assert.Contains($"--{label}", error.Message);
        Assert.False(Directory.Exists(Path.Combine(root, "data")));
    }

    [Fact]
    public void RetainedLatestRequestUsesResolvedArmVersion()
    {
        string run = CreateRun();
        string path = Path.Combine(run, RetainedRunMetadataStore.FileName);
        JsonNode metadata = JsonNode.Parse(File.ReadAllText(path))!;
        metadata["configuration"]!["cliVersion"] = "latest";
        File.WriteAllText(path, metadata.ToJsonString());
        Assert.Equal(2, new AnalyticsExportDiscovery().Export(new AnalyticsDiscoveryOptions(run), _ => { }));
        Assert.All(Rows(), row => Assert.Equal("1.0.95", row.AgentVersion));
    }

    [Theory]
    [InlineData("configuration")]
    [InlineData("tasks")]
    public void IncompleteRunPlanFailsExplicitlyWithoutOutput(string property)
    {
        string run = CreateRun();
        string path = Path.Combine(run, RetainedRunMetadataStore.FileName);
        JsonNode metadata = JsonNode.Parse(File.ReadAllText(path))!;
        metadata[property] = null;
        File.WriteAllText(path, metadata.ToJsonString());
        Assert.Throws<RecoveryException>(() =>
            new AnalyticsExportDiscovery().Export(new AnalyticsDiscoveryOptions(run), _ => { }));
        Assert.False(Directory.Exists(Path.Combine(root, "data")));
    }

    [Fact]
    public void ExplicitOutputRequiresOneArmAndPreservesExplicitSubagentEvidence()
    {
        string run = CreateRun();
        string output = Path.Combine(root, "custom.jsonl");
        Assert.Equal(2, new AnalyticsExportDiscovery().Export(
            new AnalyticsDiscoveryOptions(run, Output: output, Subagents: false), _ => { }));
        Assert.All(File.ReadLines(output), line =>
            Assert.False(JsonSerializer.Deserialize<AnalyticsTrial>(line, StateJson.Options)!.Subagents));
        CreateRun(model: "other-model");
        Assert.Throws<AnalyticsEvidenceException>(() =>
            new AnalyticsExportDiscovery().Export(
                new AnalyticsDiscoveryOptions(Path.Combine(root, "jobs"), Output: output), _ => { }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CliInfersLabelsAndAcceptsExplicitFalseSubagents(bool trailingSeparator)
    {
        string run = CreateRun();
        if (trailingSeparator)
            run += Path.DirectorySeparatorChar;
        var result = await WharfAppTests.InvokeWithUserConfig(null,
            "analytics", "export", "--run-directory", run, "--subagents", "false");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("exported-trials=2", result.Output);
        Assert.All(Rows(), row => Assert.False(row.Subagents));
    }

    private AnalyticsTrial[] Rows() =>
        File.ReadLines(Path.Combine(root, "data", $"{Arm}.jsonl"))
            .Select(line => JsonSerializer.Deserialize<AnalyticsTrial>(line, StateJson.Options)!).ToArray();

    private string CreateRun(
        string job = "all-1", string model = "test-model", int k = 2,
        bool completed = true, bool native = true)
    {
        string fixture = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/harbor"));
        HarborTask task = new HarborTaskLoader().Load(fixture)[0];
        string run = Path.Combine(root, "jobs", $"copilot-{model}-max-1.0.95", job);
        Directory.CreateDirectory(run);
        var configuration = new RunConfiguration(
            k, 1, 1, new PhaseTimeouts(), new RetryPolicy(), "text", "jobs",
            model, "max", "1.0.95", [], [], 30, false, "default", new SecretSettings());
        if (native)
            RetainedRunMetadataStore.Write(run, Path.Combine(root, "absent-tasks"), configuration, [task]);
        File.WriteAllText(Path.Combine(run, "run.jsonl"), JsonSerializer.Serialize(new
        {
            message = $"plan tasks=1 attempts={k} runs={k} task-concurrency=1 preparation-concurrency=1 admission-order=longest-first"
        }));
        for (int attempt = 1; attempt <= (completed ? k : k - 1); attempt++)
        {
            string trial = Path.Combine(run,
                RetainedRunResumePlanner.TrialDirectoryName(task.TaskId, task.SourceChecksum, attempt, k));
            Directory.CreateDirectory(trial);
            File.WriteAllText(Path.Combine(trial, "result.json"), JsonSerializer.Serialize(new
            {
                taskId = task.TaskId,
                attempt,
                status = "failed",
                language = task.Language
            }));
        }
        return run;
    }
}
