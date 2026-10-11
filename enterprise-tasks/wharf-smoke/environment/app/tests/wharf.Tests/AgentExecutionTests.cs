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
