using System.Collections.Concurrent;
using System.Text.Json;
using Wharf.Configuration;
using Wharf.Image;
using Wharf.Logging;
using Wharf.Recovery;
using Wharf.Reporting;
using Wharf.Run;
using Wharf.Tasks;

namespace Wharf.Tests;

public sealed class BaselineRunTests
{
    [Theory]
    [InlineData(true, false, "baseline-no-op")]
    [InlineData(false, true, "baseline-oracle")]
    [InlineData(true, true, "baseline-no-op-oracle")]
    public void ModesHaveDistinctJobIdentities(bool noOp, bool oracle, string expected)
    {
        Assert.Equal(expected, BaselineRunService.BuildJobDirectoryName(Configuration(noOp, oracle)));
    }

    [Fact]
    public void NoOpRunsRespectConcurrencyRetainEvidenceAndDoNotGrade()
    {
        using var fixture = new Fixture();
        RunConfiguration configuration = Configuration(true, false) with { K = 4, N = 2, P = 1 };
        var images = new FakeImages();
        var runner = new FakeRunner();
        using WharfLogger logger = WharfLogger.CreateDeferred(new WharfConfiguration(Quiet: true));

        RunResult result = new BaselineRunService(images, runner).Execute(
            fixture.Root, fixture.Output, configuration, logger, [fixture.Task],
            "all", CancellationToken.None, null);

        string run = Path.Combine(result.JobDirectory, result.AttemptDirectory);
        string[] terminals = Directory.GetDirectories(run).Select(p => Path.Combine(p, "result.json")).ToArray();
        Assert.Equal(4, terminals.Length);
        Assert.Equal(1, images.MaximumActive);
        Assert.All(images.Versions, version => Assert.Null(version));
        Assert.Equal(4, images.Removed.Count);
        Assert.Equal(4, runner.Cleanups);
        foreach (string terminal in terminals)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(terminal));
            Assert.Equal("completed", document.RootElement.GetProperty("status").GetString());
            Assert.Equal(0, document.RootElement.GetProperty("totalCost").GetDecimal());
            Assert.False(document.RootElement.GetProperty("graded").GetBoolean());
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("metrics").ValueKind);
            Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(terminal)!, "workspace")));
            Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(terminal)!, "agent")));
        }
        RunProgress progress = ProgressService.Read(Path.Combine(run, "run.jsonl"));
        Assert.Equal(4, progress.TestsCompleted);
        Assert.Equal(0, progress.MissingGraderResults);
        Assert.Equal(0, progress.FailedTests);
        Assert.Null(progress.PassPercent);
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(Path.Combine(run, "baseline-summary.json")));
        Assert.Equal(JsonValueKind.Null, summary.RootElement.GetProperty("passAt1").ValueKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OracleGradesAPristineWorkspaceAndBothFlagsRemainOneTrial(bool noOp)
    {
        using var fixture = new Fixture(solution: true);
        var runner = new FakeRunner();
        using WharfLogger logger = WharfLogger.CreateDeferred(new WharfConfiguration(Quiet: true));
        RunResult result = new BaselineRunService(new FakeImages(), runner).Execute(
            fixture.Root, fixture.Output, Configuration(noOp, true), logger, [fixture.Task],
            "all", CancellationToken.None, null);

        string run = Path.Combine(result.JobDirectory, result.AttemptDirectory);
        string trial = Assert.Single(Directory.GetDirectories(run));
        using JsonDocument terminal = JsonDocument.Parse(File.ReadAllText(Path.Combine(trial, "result.json")));
        Assert.Equal("passed", terminal.RootElement.GetProperty("status").GetString());
        Assert.True(terminal.RootElement.GetProperty("graded").GetBoolean());
        Assert.Equal(noOp ? new[] { "no-op", "oracle" } : ["oracle"], runner.Steps.ToArray());
        Assert.False(File.Exists(Path.Combine(fixture.Task.RootPath, "environment", "app", "proof.txt")));
        using JsonDocument verification = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(trial, "verifier", "trial-result.json")));
        Assert.False(Directory.Exists(verification.RootElement.GetProperty("workspaceDirectory").GetString()));
        Assert.Equal(64, terminal.RootElement.GetProperty("solutionChecksum").GetString()!.Length);
    }

    [Fact]
    public void MissingOracleSolutionFailsBeforeCreatingOutputOrPreparingImages()
    {
        using var fixture = new Fixture();
        var images = new FakeImages();
        using WharfLogger logger = WharfLogger.CreateDeferred(new WharfConfiguration(Quiet: true));
        Assert.Throws<RunException>(() => new BaselineRunService(images, new FakeRunner()).Execute(
            fixture.Root, fixture.Output, Configuration(false, true), logger, [fixture.Task],
            "all", CancellationToken.None, null));
        Assert.False(Directory.Exists(fixture.Output));
        Assert.Empty(images.Versions);
    }

    [Fact]
    public void OracleWithNoSourceChangesStillRunsGrading()
    {
        using var fixture = new Fixture(solution: true);
        using WharfLogger logger = WharfLogger.CreateDeferred(new WharfConfiguration(Quiet: true));
        RunResult result = new BaselineRunService(new FakeImages(), new FakeRunner { EmptyPatch = true }).Execute(
            fixture.Root, fixture.Output, Configuration(false, true), logger, [fixture.Task],
            "all", CancellationToken.None, null);
        string trial = Assert.Single(Directory.GetDirectories(Path.Combine(result.JobDirectory, result.AttemptDirectory)));
        using JsonDocument terminal = JsonDocument.Parse(File.ReadAllText(Path.Combine(trial, "result.json")));
        Assert.True(terminal.RootElement.GetProperty("graded").GetBoolean());
        Assert.True(File.Exists(Path.Combine(trial, "verifier", "trial-result.json")));
    }

    [Fact]
    public void FailedBaselineCommandRetainsFailureAndStillCleansResources()
    {
        using var fixture = new Fixture();
        var images = new FakeImages();
        var runner = new FakeRunner { ExitCode = 17 };
        using WharfLogger logger = WharfLogger.CreateDeferred(new WharfConfiguration(Quiet: true));
        RunResult result = new BaselineRunService(images, runner).Execute(
            fixture.Root, fixture.Output, Configuration(true, false), logger, [fixture.Task],
            "all", CancellationToken.None, null);
        string trial = Assert.Single(Directory.GetDirectories(Path.Combine(result.JobDirectory, result.AttemptDirectory)));
        using JsonDocument terminal = JsonDocument.Parse(File.ReadAllText(Path.Combine(trial, "result.json")));
        Assert.Equal("failed", terminal.RootElement.GetProperty("status").GetString());
        Assert.Contains("17", terminal.RootElement.GetProperty("error").GetString());
        Assert.Single(images.Removed);
        Assert.Equal(1, runner.Cleanups);
        Assert.True(File.Exists(Path.Combine(trial, "baseline", "no-op-result.json")));
        Assert.Equal(0, ProgressService.Read(Path.Combine(result.JobDirectory, result.AttemptDirectory, "run.jsonl"))
            .MissingGraderResults);
    }

    [Fact]
    public void BaselineTimeoutIsReportedAsATimeoutRatherThanAGradingFailure()
    {
        using var fixture = new Fixture();
        using WharfLogger logger = WharfLogger.CreateDeferred(new WharfConfiguration(Quiet: true));
        RunResult result = new BaselineRunService(
            new FakeImages(), new FakeRunner { TimedOut = true, ExitCode = 124 }).Execute(
                fixture.Root, fixture.Output, Configuration(true, false), logger, [fixture.Task],
                "all", CancellationToken.None, null);
        RunProgress progress = ProgressService.Read(Path.Combine(result.JobDirectory, result.AttemptDirectory, "run.jsonl"));
        Assert.Equal(1, progress.Timeouts);
        Assert.Equal(0, progress.MissingGraderResults);
    }

    [Fact]
    public void BaselineEvidenceCannotBeExportedAsModelScores()
    {
        using var fixture = new Fixture();
        using WharfLogger logger = WharfLogger.CreateDeferred(new WharfConfiguration(Quiet: true));
        RunResult result = new BaselineRunService(new FakeImages(), new FakeRunner()).Execute(
            fixture.Root, fixture.Output, Configuration(true, false), logger, [fixture.Task],
            "all", CancellationToken.None, null);
        string run = Path.Combine(result.JobDirectory, result.AttemptDirectory);
        AnalyticsEvidenceException exception = Assert.Throws<AnalyticsEvidenceException>(() =>
            new AnalyticsExporter().Export(run, Path.Combine(fixture.Root, "scores.jsonl"),
                new AnalyticsExportOptions("baseline", "copilot-cli", "1.0.105", "unused", "default", false)));
        Assert.Contains("baseline-summary.json", exception.Message);
        Assert.False(File.Exists(Path.Combine(fixture.Root, "scores.jsonl")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0,1")]
    public void DockerBaselineUsesExplicitOwnershipLimitsAndNoCredentials(string? cpuSet)
    {
        var request = new BaselineRequest("task", "agent-task:latest", "agent-baseline",
            "/evidence", "/artifacts", "/workspace", null, TimeSpan.FromSeconds(10), false, cpuSet);
        IReadOnlyList<string> arguments = DockerBaselineRunner.BuildCreateArguments(request);
        Assert.Contains("com.bartr.wharf.managed=true", arguments);
        Assert.Contains("1000:1000", arguments);
        Assert.Contains("8g", arguments);
        Assert.Equal(cpuSet is not null, arguments.Contains("--cpuset-cpus"));
        Assert.DoesNotContain("--volume", arguments);
        Assert.DoesNotContain("--env", arguments);
        Assert.Contains("no-op.stdout.log",
            DockerBaselineRunner.BuildExecutionArguments(request, "no-op").Last());
        Assert.Throws<RunException>(() => DockerBaselineRunner.BuildExecutionArguments(request, "oracle"));
    }

    [Fact]
    public void CancellationRetainsTerminalEvidenceAndCleansTheContainer()
    {
        using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        var images = new FakeImages();
        var runner = new FakeRunner { OnExecution = cancellation.Cancel };
        using WharfLogger logger = WharfLogger.CreateDeferred(new WharfConfiguration(Quiet: true));
        Assert.ThrowsAny<OperationCanceledException>(() =>
            new BaselineRunService(images, runner).Execute(
                fixture.Root, fixture.Output, Configuration(true, false), logger, [fixture.Task],
                "all", cancellation.Token, null));
        string terminalPath = Assert.Single(Directory.GetFiles(fixture.Output,
            "result.json", SearchOption.AllDirectories));
        using JsonDocument terminal = JsonDocument.Parse(File.ReadAllText(terminalPath));
        Assert.Equal("cancelled", terminal.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, runner.Cleanups);
        Assert.Single(images.Removed);
    }

    [Fact]
    public void RetainedVerifierRetriesPreserveTheirIdentityAndRejectGaps()
    {
        using var fixture = new Fixture();
        Assert.Equal(0, BaselineRunService.RetainedVerifierRetryCount(fixture.Root));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "verifier-retries", "retry-1"));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "verifier-retries", "retry-2"));
        Assert.Equal(2, BaselineRunService.RetainedVerifierRetryCount(fixture.Root));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "verifier-retries", "retry-4"));
        Assert.Throws<RunException>(() => BaselineRunService.RetainedVerifierRetryCount(fixture.Root));
    }

    private static RunConfiguration Configuration(bool noOp, bool oracle) =>
        RunConfigurationResolver.Resolve(null,
            new RunConfigurationOverrides(NoOp: noOp, Oracle: oracle, CliVersion: "unused"),
            new Dictionary<string, string?>());

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"wharf-baseline-test-{Guid.NewGuid():N}");
        public string Output => Path.Combine(Root, "output");
        public HarborTask Task { get; }

        public Fixture(bool solution = false)
        {
            string task = Path.Combine(Root, "task");
            Directory.CreateDirectory(Path.Combine(task, "environment", "app"));
            string instruction = Path.Combine(task, "instruction.md");
            File.WriteAllText(instruction, "Synthetic baseline fixture.");
            if (solution)
            {
                Directory.CreateDirectory(Path.Combine(task, "solution"));
                File.WriteAllText(Path.Combine(task, "solution", "solve.sh"), "true\n");
            }
            Task = new HarborTask("baseline-task", "baseline-task", "synthetic", "synthetic",
                "test", "shell", "1.3", task, instruction, new string('a', 64),
                null, null, null, null, 30, 30, 30, 30);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed class FakeRunner : IBaselineRunner
    {
        public ConcurrentQueue<string> Steps { get; } = new();
        public int ExitCode { get; init; }
        public bool EmptyPatch { get; init; }
        public bool TimedOut { get; init; }
        public Action? OnExecution { get; init; }
        public int Cleanups;

        public Task PrepareAsync(BaselineRequest request, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<BaselineCommandResult> ExecuteAsync(
            BaselineRequest request, string step, CancellationToken cancellationToken)
        {
            Steps.Enqueue(step);
            OnExecution?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            if (step == "oracle")
            {
                File.WriteAllText(Path.Combine(request.ArtifactsDirectory, "model.patch"),
                    EmptyPatch ? "" : "diff --git a/proof.txt b/proof.txt\nnew file mode 100644\n" +
                    "--- /dev/null\n+++ b/proof.txt\n@@ -0,0 +1 @@\n+oracle\n");
            }
            return Task.FromResult(new BaselineCommandResult(step, ExitCode, 1, TimedOut));
        }

        public Task CleanupAsync(BaselineRequest request)
        {
            Interlocked.Increment(ref Cleanups);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeImages : IAgentImagePreparer
    {
        public ConcurrentBag<string?> Versions { get; } = [];
        public ConcurrentBag<string> Removed { get; } = [];
        private int active;
        public int MaximumActive;

        public async Task<string> PrepareAsync(HarborTask task, string evidenceDirectory,
            string imageName, Action<string>? progress = null, CancellationToken cancellationToken = default,
            string? copilotVersion = null)
        {
            Versions.Add(copilotVersion);
            int current = Interlocked.Increment(ref active);
            MaximumActive = Math.Max(MaximumActive, current);
            try
            {
                await Task.Delay(10, cancellationToken);
                return imageName;
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        }

        public Task RemoveImageAsync(string image, CancellationToken cancellationToken = default)
        {
            Removed.Add(image);
            return Task.CompletedTask;
        }
    }
}
