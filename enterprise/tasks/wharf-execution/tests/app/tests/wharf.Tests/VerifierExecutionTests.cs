using Wharf.Verifier;

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
    public void RejectsAgentWorkspaceReuse()
    {
        using TemporaryDirectory source = new();
        VerifierRequest request = Request(source.Path, source.Path);

        Assert.Throws<VerifierExecutionException>(request.Validate);
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
        public Task<VerifierCommandResult> RunAsync(
            VerifierRequest request,
            string workspaceDirectory,
            VerifierCommand command,
            CancellationToken cancellationToken) =>
            Task.FromResult(new VerifierCommandResult(
                command.Name,
                fail ? 1 : 0,
                "stdout",
                fail ? "failed" : "",
                1,
                false));
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
