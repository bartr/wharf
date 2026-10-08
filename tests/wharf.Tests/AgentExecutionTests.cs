using Wharf.Agent;
using Wharf.Copilot;
using Wharf.Execution;
using Wharf.Run;

namespace Wharf.Tests;

public sealed class AgentExecutionTests
{
    [Fact]
    public async Task CapturesPatchTrajectoryLogsTimingAndUsage()
    {
        using TemporaryDirectory source = new();
        using TemporaryDirectory workspace = new();
        using TemporaryDirectory evidence = new();
        var runner = new FakeAgentRunner(new AgentRunResult(
            0, "stdout", "stderr", """{"event":"step"}""",
            "diff --git a/file b/file", TimeSpan.FromMilliseconds(12), 100, 3, 2, 0.04m));
        var service = new AgentExecutionService(runner);

        AgentEvidenceSummary summary = await service.ExecuteAsync(
            Request(source.Path, workspace.Path),
            evidence.Path);

        Assert.Equal("completed", summary.Status);
        Assert.Equal(100, summary.Tokens);
        Assert.Equal("diff --git a/file b/file", File.ReadAllText(Path.Combine(evidence.Path, "patch.diff")));
        Assert.True(File.Exists(Path.Combine(evidence.Path, "result.json")));
    }

    [Fact]
    public async Task EmptyPatchIsUnfinishedAndEvidenceIsRetained()
    {
        using TemporaryDirectory source = new();
        using TemporaryDirectory workspace = new();
        using TemporaryDirectory evidence = new();
        var service = new AgentExecutionService(
            new FakeAgentRunner(new AgentRunResult(0, "out", "", "trace", "", TimeSpan.Zero)));

        await Assert.ThrowsAsync<AgentExecutionException>(
            () => service.ExecuteAsync(Request(source.Path, workspace.Path), evidence.Path));

        Assert.Contains("\"status\":\"unfinished\"", File.ReadAllText(Path.Combine(evidence.Path, "result.json")));
    }

    [Fact]
    public async Task CgroupOomFailsWithTypedRetainedError()
    {
        using TemporaryDirectory source = new();
        using TemporaryDirectory workspace = new();
        using TemporaryDirectory evidence = new();
        var service = new AgentExecutionService(
            new FakeAgentRunner(new AgentRunResult(
                137,
                "partial output",
                "",
                "trace",
                "",
                TimeSpan.FromSeconds(3),
                OutOfMemory: true)));

        AgentEvidenceSummary summary = await service.ExecuteAsync(
            Request(source.Path, workspace.Path),
            evidence.Path);

        Assert.Equal("failed", summary.Status);
        Assert.Equal("OutOfMemory", summary.ErrorType);
        Assert.Equal("agent container reported a cgroup OOM kill", summary.Error);
        Assert.Contains("\"schemaVersion\":\"1.1\"", File.ReadAllText(
            Path.Combine(evidence.Path, "result.json")));
    }

    [Fact]
    public void RejectsSharedAgentAndVerifierWorkspace()
    {
        using TemporaryDirectory source = new();
        var request = Request(source.Path, source.Path);

        Assert.Throws<AgentExecutionException>(request.Validate);
    }

    [Fact]
    public void RejectsHiddenMaterialInsideAgentMount()
    {
        using TemporaryDirectory source = new();
        using TemporaryDirectory workspace = new();
        string hidden = Path.Combine(workspace.Path, "hidden-tests");
        Directory.CreateDirectory(hidden);

        Assert.Throws<AgentExecutionException>(
            () => Request(source.Path, workspace.Path, [hidden]).Validate());
    }

    [Fact]
    public void InitializesWorkspaceAsRootThenAssignsAgentOwnership()
    {
        using TemporaryDirectory source = new();
        using TemporaryDirectory workspace = new();
        AgentExecutionRequest request = Request(source.Path, workspace.Path);

        IReadOnlyList<string> arguments =
            DockerAgentWorkspacePreparer.BuildWorkspaceInitializationArguments(request, "task-init");

        AssertArgumentsContain(arguments, "--name", "task-init");
        AssertArgumentsContain(arguments, "--label", "com.bartr.wharf.managed=true");
        AssertArgumentsContain(arguments, "--user", "0:0");
        AssertArgumentsContain(arguments, "--network", "none");
        Assert.Contains("cp -a /app/. /workspace/ && chown -R 1000:1000 /workspace", arguments);
    }

    [Fact]
    public void InstallsCopilotAsUid1000DuringWorkspacePreparation()
    {
        using TemporaryDirectory source = new();
        using TemporaryDirectory workspace = new();
        AgentExecutionRequest request = Request(source.Path, workspace.Path);

        IReadOnlyList<string> arguments =
            DockerAgentWorkspacePreparer.BuildWorkspaceSetupArguments(request, "task-setup");

        AssertArgumentsContain(arguments, "--name", "task-setup");
        AssertArgumentsContain(arguments, "--user", "1000:1000");
        Assert.Contains(arguments, argument =>
            argument.Contains("npm install", StringComparison.Ordinal) &&
            argument.Contains("@github/copilot@1.2.3", StringComparison.Ordinal));
    }

    [Fact]
    public void RunsAgentAsUid1000WithoutCopyingImageContent()
    {
        using TemporaryDirectory source = new();
        using TemporaryDirectory workspace = new();
        AgentExecutionRequest request = Request(source.Path, workspace.Path);

        IReadOnlyList<string> arguments =
            DockerAgentRunner.BuildAgentArguments(request, "task-agent");

        AssertArgumentsContain(arguments, "--name", "task-agent");
        AssertArgumentsContain(arguments, "--user", "1000:1000");
        AssertArgumentsContain(arguments, "--memory", "8g");
        AssertArgumentsContain(arguments, "--memory-swap", "8g");
        AssertArgumentsContain(arguments, "--pids-limit", "512");
        int pnpmEnvironmentIndex = arguments.ToList().IndexOf(
            "NPM_CONFIG_MANAGE_PACKAGE_MANAGER_VERSIONS=false");
        Assert.True(pnpmEnvironmentIndex > 0);
        Assert.Equal("--env", arguments[pnpmEnvironmentIndex - 1]);
        Assert.DoesNotContain(arguments, argument => argument.Contains("cp -a /app", StringComparison.Ordinal));
        Assert.DoesNotContain(arguments, argument => argument.Contains("npm install", StringComparison.Ordinal));
    }

    [Fact]
    public void AttachesLeadingDashPromptToOption()
    {
        string argument = RunService.BuildPromptArgument("- Update the display property.");

        Assert.Equal("--prompt=- Update the display property.", argument);
    }

    [Fact]
    public async Task RetriesCopilotInternalServerFailureWithSeparateEvidence()
    {
        using TemporaryDirectory source = new();
        using TemporaryDirectory workspace = new();
        using TemporaryDirectory retryWorkspace = new();
        using TemporaryDirectory evidence = new();
        var runner = new SequenceAgentRunner(
            new AgentRunResult(
                1,
                "first",
                "Execution failed: 400 internal server error (Request ID: test)",
                "first trace",
                "first patch",
                TimeSpan.FromSeconds(1),
                Cost: 2m),
            new AgentRunResult(
                0,
                "second",
                "",
                "second trace",
                "second patch",
                TimeSpan.FromSeconds(1),
                Cost: 3m));
        var service = new AgentExecutionService(runner);
        var sink = new RecordingRetrySink();

        AgentExecutionOutcome outcome = await service.ExecuteWithInfrastructureRetryAsync(
            Request(source.Path, workspace.Path),
            evidence.Path,
            "task-1__attempt",
            1,
            (request, retryNumber, cancellationToken) => Task.FromResult(
                request with { WorkspaceDirectory = retryWorkspace.Path }),
            sink);

        Assert.Equal("completed", outcome.Summary.Status);
        Assert.Equal(retryWorkspace.Path, outcome.Request.WorkspaceDirectory);
        Assert.Equal(5m, outcome.TotalCost);
        Assert.Equal(1, outcome.InfrastructureRetries);
        Assert.Single(sink.Records);
        Assert.Equal("copilot-service", sink.Records[0].Classification);
        string retryDirectory = Assert.Single(
            Directory.GetDirectories(Path.Combine(evidence.Path, "infrastructure-retries")));
        Assert.Equal("first patch", File.ReadAllText(Path.Combine(retryDirectory, "patch.diff")));
        Assert.Equal("second patch", File.ReadAllText(Path.Combine(evidence.Path, "patch.diff")));
    }

    [Fact]
    public void DoesNotRetryInvalidPromptFailure()
    {
        Assert.False(AgentExecutionService.IsRetryableCopilotServiceFailure(
            "error: Invalid command format."));
    }

    [Fact]
    public async Task RetriesCopilotInternalServerFailureWithoutPatch()
    {
        using TemporaryDirectory source = new();
        using TemporaryDirectory workspace = new();
        using TemporaryDirectory retryWorkspace = new();
        using TemporaryDirectory evidence = new();
        var service = new AgentExecutionService(new SequenceAgentRunner(
            new AgentRunResult(
                1,
                "",
                "Execution failed: 400 internal server error",
                "",
                "",
                TimeSpan.Zero),
            new AgentRunResult(
                0,
                "",
                "",
                "",
                "successful retry patch",
                TimeSpan.Zero)));

        AgentExecutionOutcome outcome = await service.ExecuteWithInfrastructureRetryAsync(
            Request(source.Path, workspace.Path),
            evidence.Path,
            "task-1__attempt",
            1,
            (request, retryNumber, cancellationToken) => Task.FromResult(
                request with { WorkspaceDirectory = retryWorkspace.Path }),
            new RecordingRetrySink());

        Assert.Equal("completed", outcome.Summary.Status);
        Assert.Equal(1, outcome.InfrastructureRetries);
    }

    [Fact]
    public void ReportsWorkspaceInitializationFailureWithDockerDiagnostic()
    {
        var result = new DockerProcessResult(
            1,
            "",
            "cp: cannot open '/app/locked': Permission denied");

        AgentExecutionException exception = Assert.Throws<AgentExecutionException>(
            () => DockerAgentWorkspacePreparer.EnsureSuccessful(
                result,
                "agent workspace initialization"));

        Assert.Contains("agent workspace initialization failed", exception.Message);
        Assert.Contains("Permission denied", exception.Message);
    }

    private static void AssertArgumentsContain(
        IReadOnlyList<string> arguments,
        string option,
        string value)
    {
        int index = arguments.ToList().IndexOf(option);
        Assert.True(index >= 0, $"Missing Docker option {option}");
        Assert.True(index + 1 < arguments.Count, $"Missing value for Docker option {option}");
        Assert.Equal(value, arguments[index + 1]);
    }

    private static AgentExecutionRequest Request(
        string source,
        string workspace,
        IReadOnlyList<string>? hidden = null) =>
        new(
            "task-1",
            "agent:1",
            source,
            workspace,
            "wharf-agent",
            new CopilotInvocation(
                "copilot",
                ["--model", "test"],
                new Dictionary<string, string>(),
                false,
                "1.2.3",
                "1.2.3",
                "test",
                "medium",
                "medium",
                [],
                []),
            hidden ?? []);

    private sealed class FakeAgentRunner(AgentRunResult result) : IAgentRunner
    {
        public Task<AgentRunResult> RunAsync(
            AgentExecutionRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class SequenceAgentRunner(params AgentRunResult[] results) : IAgentRunner
    {
        private readonly Queue<AgentRunResult> remaining = new(results);

        public Task<AgentRunResult> RunAsync(
            AgentExecutionRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(remaining.Dequeue());
    }

    private sealed class RecordingRetrySink : IInfrastructureRetrySink
    {
        public List<InfrastructureRetryEvidence> Records { get; } = [];

        public Task RecordAsync(
            InfrastructureRetryEvidence evidence,
            CancellationToken cancellationToken)
        {
            Records.Add(evidence);
            return Task.CompletedTask;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"wharf-agent-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
