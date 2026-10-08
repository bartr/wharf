using Wharf.Agent;
using Wharf.Copilot;

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
        AssertArgumentsContain(arguments, "--memory", "4g");
        AssertArgumentsContain(arguments, "--memory-swap", "4g");
        AssertArgumentsContain(arguments, "--pids-limit", "512");
        int pnpmEnvironmentIndex = arguments.ToList().IndexOf(
            "NPM_CONFIG_MANAGE_PACKAGE_MANAGER_VERSIONS=false");
        Assert.True(pnpmEnvironmentIndex > 0);
        Assert.Equal("--env", arguments[pnpmEnvironmentIndex - 1]);
        Assert.DoesNotContain(arguments, argument => argument.Contains("cp -a /app", StringComparison.Ordinal));
        Assert.DoesNotContain(arguments, argument => argument.Contains("npm install", StringComparison.Ordinal));
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
