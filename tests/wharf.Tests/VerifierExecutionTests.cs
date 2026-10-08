using Wharf.Verifier;
using Wharf.Run;

namespace Wharf.Tests;

public sealed class VerifierExecutionTests
{
    [Fact]
    public async Task CreatesPristineWorkspaceAppliesPatchAndPersistsPassingTrial()
    {
        using TemporaryDirectory source = new();
        using TemporaryDirectory agent = new();
        using TemporaryDirectory evidence = new();
        var factory = new FakeWorkspaceFactory(source.Path);
        var applier = new RecordingPatchApplier();
        var runner = new FakeVerifierRunner();
        var service = new PristineVerifierService(factory, applier, runner);

        TrialResult result = await service.VerifyAsync(
            Request(source.Path, agent.Path),
            "diff --git a/file b/file",
            evidence.Path);

        Assert.Equal("passed", result.Status);
        Assert.NotEqual(agent.Path, result.WorkspaceDirectory);
        Assert.Equal("diff --git a/file b/file", applier.Patch);
        Assert.True(File.Exists(Path.Combine(evidence.Path, "trial-result.json")));
        Assert.True(File.Exists(Path.Combine(evidence.Path, "check.stdout.log")));
    }

    [Fact]
    public async Task PreparationDoesNotRunVerifierCommands()
    {
        using TemporaryDirectory source = new();
        using TemporaryDirectory agent = new();
        using TemporaryDirectory evidence = new();
        var runner = new FakeVerifierRunner();
        var service = new PristineVerifierService(
            new FakeWorkspaceFactory(source.Path),
            new RecordingPatchApplier(),
            runner);

        PreparedVerifierRun prepared = await service.PrepareAsync(
            Request(source.Path, agent.Path),
            "patch",
            evidence.Path);

        Assert.Equal(0, runner.CallCount);
        TrialResult result = await service.VerifyPreparedAsync(prepared);
        Assert.Equal("passed", result.Status);
        Assert.Equal(1, runner.CallCount);
    }

    [Fact]
    public async Task VerifierCommandDoesNotHoldPreparationPermit()
    {
        using TemporaryDirectory source = new();
        using TemporaryDirectory agent = new();
        using TemporaryDirectory evidence = new();
        var runner = new BlockingVerifierRunner();
        var service = new PristineVerifierService(
            new FakeWorkspaceFactory(source.Path),
            new RecordingPatchApplier(),
            runner);
        await using var gate = new PreparationGate(1);
        PreparedVerifierRun prepared = await gate.ExecuteAsync(
            PreparationOperation.Verifier,
            cancellationToken => service.PrepareAsync(
                Request(source.Path, agent.Path),
                "patch",
                evidence.Path,
                cancellationToken),
            CancellationToken.None);

        Task<TrialResult> verification = service.VerifyPreparedAsync(prepared);
        await runner.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        int agentPreparation = await gate.ExecuteAsync(
            PreparationOperation.Agent,
            _ => Task.FromResult(42),
            CancellationToken.None);

        Assert.Equal(42, agentPreparation);
        Assert.False(verification.IsCompleted);
        runner.Release.SetResult();
        Assert.Equal("passed", (await verification).Status);
    }

    [Fact]
    public async Task StopsAfterFailingVerifierAndRetainsRawEvidence()
    {
        using TemporaryDirectory source = new();
        using TemporaryDirectory agent = new();
        using TemporaryDirectory evidence = new();
        var runner = new FakeVerifierRunner(fail: true);
        var service = new PristineVerifierService(
            new FakeWorkspaceFactory(source.Path),
            new RecordingPatchApplier(),
            runner);

        TrialResult result = await service.VerifyAsync(
            Request(source.Path, agent.Path) with
            {
                Commands =
                [
                    new VerifierCommand("check", "false", TimeSpan.FromSeconds(1)),
                    new VerifierCommand("never", "true", TimeSpan.FromSeconds(1))
                ]
            },
            "patch",
            evidence.Path);

        Assert.Equal("failed", result.Status);
        Assert.Single(result.Commands);
        Assert.True(File.Exists(Path.Combine(evidence.Path, "check.stderr.log")));
    }

    [Fact]
    public async Task RejectsEmptyPatchBeforeCreatingTrial()
    {
        using TemporaryDirectory source = new();
        using TemporaryDirectory agent = new();
        using TemporaryDirectory evidence = new();
        var service = new PristineVerifierService(
            new FakeWorkspaceFactory(source.Path),
            new RecordingPatchApplier(),
            new FakeVerifierRunner());

        await Assert.ThrowsAsync<VerifierExecutionException>(
            () => service.VerifyAsync(Request(source.Path, agent.Path), "", evidence.Path));
        Assert.Empty(Directory.EnumerateFileSystemEntries(evidence.Path));
    }

    [Fact]
    public async Task RewardZeroFailsTrialWhenVerifierCommandSucceeds()
    {
        using TemporaryDirectory source = new();
        using TemporaryDirectory agent = new();
        using TemporaryDirectory evidence = new();
        var service = new PristineVerifierService(
            new FakeWorkspaceFactory(source.Path),
            new RecordingPatchApplier(),
            new RewardWritingVerifierRunner(evidence.Path, reward: 0));

        TrialResult result = await service.VerifyAsync(
            Request(source.Path, agent.Path),
            "patch",
            evidence.Path);

        Assert.Equal("failed", result.Status);
        Assert.Equal(0, result.Metrics?.PassAt);
    }

    [Theory]
    [InlineData(137, false, "VerifierInfrastructure")]
    [InlineData(143, false, "VerifierInfrastructure")]
    [InlineData(137, true, "VerifierTimeout")]
    public async Task RetainsEvidenceBeforeReportingRetryableVerifierFailure(
        int exitCode,
        bool timedOut,
        string expectedErrorType)
    {
        using TemporaryDirectory source = new();
        using TemporaryDirectory agent = new();
        using TemporaryDirectory evidence = new();
        var service = new PristineVerifierService(
            new FakeWorkspaceFactory(source.Path),
            new RecordingPatchApplier(),
            new FixedResultVerifierRunner(exitCode, timedOut));

        VerifierExecutionException exception =
            await Assert.ThrowsAsync<VerifierExecutionException>(
                () => service.VerifyAsync(
                    Request(source.Path, agent.Path),
                    "patch",
                    evidence.Path));

        Assert.Equal(expectedErrorType, exception.ErrorType);
        Assert.True(File.Exists(Path.Combine(evidence.Path, "check.stdout.log")));
        Assert.True(File.Exists(Path.Combine(evidence.Path, "patch.diff")));
        Assert.True(File.Exists(Path.Combine(evidence.Path, "trial-result.json")));
    }

    [Fact]
    public void RejectsAgentWorkspaceReuse()
    {
        using TemporaryDirectory source = new();
        VerifierRequest request = Request(source.Path, source.Path);

        Assert.Throws<VerifierExecutionException>(request.Validate);
    }

    [Fact]
    public void LimitsDockerVerifierMemoryWithoutAdditionalSwap()
    {
        using TemporaryDirectory source = new();
        using TemporaryDirectory agent = new();
        using TemporaryDirectory evidence = new();
        using TemporaryDirectory artifacts = new();
        VerifierRequest request = Request(source.Path, agent.Path) with
        {
            Image = "verifier:1",
            EvidenceDirectory = evidence.Path,
            ArtifactsDirectory = artifacts.Path
        };

        IReadOnlyList<string> arguments = DockerVerifierRunner.BuildArguments(
            request,
            request.Commands.Single(),
            "task-verifier");

        AssertArgumentsContain(arguments, "--name", "task-verifier");
        AssertArgumentsContain(arguments, "--memory", "8g");
        Assert.DoesNotContain("--memory-swap", arguments);
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

    private static VerifierRequest Request(string source, string agent) =>
        new(
            "task-1",
            source,
            agent,
            "wharf-verifier",
            [new VerifierCommand("check", "true", TimeSpan.FromSeconds(1))],
            [],
            ["VERIFIER_TOKEN"]);

    private sealed class FakeWorkspaceFactory(string source) : IPristineWorkspaceFactory
    {
        public Task<string> CreateAsync(string sourceDirectory, CancellationToken cancellationToken)
        {
            string workspace = Path.Combine(
                Path.GetTempPath(),
                $"wharf-pristine-{Guid.NewGuid():N}");
            Directory.CreateDirectory(workspace);
            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(workspace, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }

            return Task.FromResult(workspace);
        }
    }

    private sealed class RecordingPatchApplier : IPatchApplier
    {
        public string? Patch { get; private set; }

        public Task ApplyAsync(
            string workspaceDirectory,
            string patch,
            CancellationToken cancellationToken)
        {
            Patch = patch;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeVerifierRunner(bool fail = false) : IVerifierRunner
    {
        public int CallCount { get; private set; }

        public Task<VerifierCommandResult> RunAsync(
            VerifierRequest request,
            string workspaceDirectory,
            VerifierCommand command,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new VerifierCommandResult(
                command.Name,
                fail ? 1 : 0,
                "stdout",
                fail ? "failed" : "",
                1,
                false));
        }
    }

    private sealed class RewardWritingVerifierRunner(
        string evidenceDirectory,
        int reward) : IVerifierRunner
    {
        public Task<VerifierCommandResult> RunAsync(
            VerifierRequest request,
            string workspaceDirectory,
            VerifierCommand command,
            CancellationToken cancellationToken)
        {
            File.WriteAllText(
                Path.Combine(evidenceDirectory, "reward.json"),
                $$"""{"reward":{{reward}},"f2p":0.5,"p2p":1.0}""");
            return Task.FromResult(new VerifierCommandResult(
                command.Name,
                0,
                "stdout",
                "",
                1,
                false));
        }
    }

    private sealed class BlockingVerifierRunner : IVerifierRunner
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<VerifierCommandResult> RunAsync(
            VerifierRequest request,
            string workspaceDirectory,
            VerifierCommand command,
            CancellationToken cancellationToken)
        {
            Started.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new VerifierCommandResult(
                command.Name,
                0,
                "stdout",
                "",
                1,
                false);
        }
    }

    private sealed class FixedResultVerifierRunner(
        int exitCode,
        bool timedOut) : IVerifierRunner
    {
        public Task<VerifierCommandResult> RunAsync(
            VerifierRequest request,
            string workspaceDirectory,
            VerifierCommand command,
            CancellationToken cancellationToken) =>
            Task.FromResult(new VerifierCommandResult(
                command.Name,
                exitCode,
                "stdout",
                "stderr",
                1,
                timedOut));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"wharf-verifier-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
