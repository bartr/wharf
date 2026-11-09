using System.Collections.Concurrent;
using Wharf.Build;
using Wharf.Configuration;
using Wharf.Copilot;
using Wharf.Image;
using Wharf.Logging;
using Wharf.Run;
using Wharf.Tasks;
using Wharf.Verifier;

namespace Wharf.Tests;

public sealed class BuildServiceTests
{
    [Fact]
    public void WarmsExactAgentAndVerifierCachesWithinCombinedConcurrency()
    {
        var tracker = new Tracker();
        var images = new FakeImages(tracker);
        var probe = new Probe();
        using var logger = WharfLogger.Create(new WharfConfiguration { Quiet = true });
        var service = new BuildService(
            imagePreparer: images, cliProbe: probe,
            prepareVerifier: async (task, directory, identity, _, token) =>
            {
                await tracker.PrepareAsync(token);
                return RunService.BuildVerifierImageName(task.TaskId, identity);
            });

        BuildResult result = service.Execute(FixtureRoot(), logger, 3);

        int count = new HarborTaskLoader().Load(FixtureRoot()).Count;
        Assert.Equal(count, result.PreparedCount);
        Assert.Equal(count, result.BuiltCount);
        Assert.Equal(count, result.VerifierPreparedCount);
        Assert.Equal("1.0.107", result.CliVersion);
        Assert.Equal(3, result.PreparationConcurrency);
        Assert.Equal(1, probe.Calls);
        Assert.InRange(tracker.Peak, 2, 3);
        Assert.Equal(count * 2, images.Removed.Count);
        Assert.All(images.Requests, request =>
        {
            Assert.Equal("1.0.107", request.Version);
            Assert.StartsWith("agent-", request.Image, StringComparison.Ordinal);
            Assert.NotEqual(request.Task.EnvironmentImage, request.Image);
            Assert.False(Directory.Exists(Path.GetDirectoryName(
                Path.GetDirectoryName(request.Evidence))!));
        });
        Assert.Contains(images.Requests, request => request.Task.EnvironmentImage is not null);
        Assert.All(images.Removed, image => Assert.True(
            image.StartsWith("agent-", StringComparison.Ordinal) ||
            image.StartsWith("verifier-", StringComparison.Ordinal)));
    }

    [Fact]
    public void DefaultsToFourAndPinnedVersionNeverLooksUpLatest()
    {
        var images = new FakeImages(new Tracker());
        var probe = new Probe();
        using var logger = WharfLogger.Create(new WharfConfiguration { Quiet = true });
        var service = new BuildService(
            imagePreparer: images, cliProbe: probe,
            prepareVerifier: (_, _, _, _, _) => Task.FromResult<string?>(null));

        BuildResult result = service.Execute(FixtureRoot(), logger, requestedCliVersion: "1.0.106");

        Assert.Equal(4, result.PreparationConcurrency);
        Assert.Equal("1.0.106", result.CliVersion);
        Assert.Equal(0, result.VerifierPreparedCount);
        Assert.Equal(0, probe.Calls);
        Assert.All(images.Requests, request => Assert.Equal("1.0.106", request.Version));
    }

    [Fact]
    public void InvalidInputDoesNotPrepareImagesOrResolveLatest()
    {
        var images = new FakeImages(new Tracker());
        var probe = new Probe();
        using var logger = WharfLogger.Create(new WharfConfiguration { Quiet = true });
        var service = new BuildService(imagePreparer: images, cliProbe: probe);

        Assert.Throws<BuildException>(() => service.Execute(FixtureRoot(), logger, 0));
        Assert.Throws<CopilotAdapterException>(() =>
            service.Execute(FixtureRoot(), logger, requestedCliVersion: "bad-version"));

        Assert.Empty(images.Requests);
        Assert.Equal(0, probe.Calls);
    }

    [Fact]
    public void FailedVerifierPreparationStillRemovesTheAgentChild()
    {
        var images = new FakeImages(new Tracker());
        using var logger = WharfLogger.Create(new WharfConfiguration { Quiet = true });
        var service = new BuildService(
            imagePreparer: images,
            prepareVerifier: (_, _, _, _, _) =>
                throw new VerifierExecutionException("fixture failure", "VerifierBuild"));

        Assert.Throws<VerifierExecutionException>(() =>
            service.Execute(FixtureRoot(), logger, 1, "1.0.107"));

        Assert.Single(images.Removed);
        Assert.StartsWith("agent-", images.Removed.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void CancellationRetainsCleanupIndependentFromTheCancelledToken()
    {
        var images = new FakeImages(new Tracker());
        using var cancellation = new CancellationTokenSource();
        using var logger = WharfLogger.Create(new WharfConfiguration { Quiet = true });
        var service = new BuildService(
            imagePreparer: images,
            prepareVerifier: (_, _, _, _, token) =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.FromResult<string?>(null);
            });

        Assert.ThrowsAny<OperationCanceledException>(() =>
            service.Execute(FixtureRoot(), logger, 1, "1.0.107", cancellation.Token));
        Assert.Single(images.Removed);
    }

    [Fact]
    public void UnexpectedParentResultIsNeverDeleted()
    {
        var images = new FakeImages(new Tracker()) { ReturnParent = true };
        using var logger = WharfLogger.Create(new WharfConfiguration { Quiet = true });
        var service = new BuildService(imagePreparer: images);

        Assert.Throws<BuildException>(() => service.Execute(FixtureRoot(), logger, 1, "1.0.107"));
        Assert.Empty(images.Removed);
    }

    [Fact]
    public void CleanupFailureStillAttemptsBothChildrenAndFailsExplicitly()
    {
        var images = new FakeImages(new Tracker()) { FailVerifierCleanup = true };
        using var logger = WharfLogger.Create(new WharfConfiguration { Quiet = true });
        var service = new BuildService(
            imagePreparer: images,
            prepareVerifier: (task, _, identity, _, _) =>
                Task.FromResult<string?>(RunService.BuildVerifierImageName(task.TaskId, identity)));

        BuildException error = Assert.Throws<BuildException>(() =>
            service.Execute(FixtureRoot(), logger, 1, "1.0.107"));

        Assert.Contains("cleanup failed", error.Message, StringComparison.Ordinal);
        Assert.Equal(2, images.Removed.Count);
        Assert.Contains(images.Removed, image => image.StartsWith("agent-", StringComparison.Ordinal));
    }

    private static string FixtureRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string path = Path.Combine(directory.FullName, "tests", "fixtures", "harbor");
            if (Directory.Exists(path))
                return path;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Harbor fixtures not found");
    }

    private sealed class Probe : ICopilotCliProbe
    {
        public int Calls { get; private set; }
        public Task<CopilotCliVersion> GetVersionAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new CopilotCliVersion("1.0.107"));
        }
    }

    private sealed class Tracker
    {
        private readonly object sync = new();
        private int active;
        public int Peak { get; private set; }
        public async Task PrepareAsync(CancellationToken token)
        {
            lock (sync)
                Peak = Math.Max(Peak, ++active);
            try
            {
                await Task.Delay(20, token);
            }
            finally
            {
                lock (sync)
                    active--;
            }
        }
    }

    private sealed record Request(HarborTask Task, string Evidence, string Image, string? Version);

    private sealed class FakeImages(Tracker tracker) : IAgentImagePreparer
    {
        public ConcurrentBag<Request> Requests { get; } = [];
        public ConcurrentBag<string> Removed { get; } = [];
        public bool ReturnParent { get; init; }
        public bool FailVerifierCleanup { get; init; }

        public async Task<string> PrepareAsync(
            HarborTask task, string evidenceDirectory, string imageName,
            Action<string>? progress = null, CancellationToken cancellationToken = default,
            string? copilotVersion = null)
        {
            Directory.CreateDirectory(evidenceDirectory);
            Requests.Add(new Request(task, evidenceDirectory, imageName, copilotVersion));
            await tracker.PrepareAsync(cancellationToken);
            return ReturnParent ? "registry.example.test/parent:latest" : imageName;
        }

        public Task RemoveImageAsync(string image, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Removed.Add(image);
            if (FailVerifierCleanup && image.StartsWith("verifier-", StringComparison.Ordinal))
                throw new AgentImageException("fixture cleanup failure");
            return Task.CompletedTask;
        }
    }
}
