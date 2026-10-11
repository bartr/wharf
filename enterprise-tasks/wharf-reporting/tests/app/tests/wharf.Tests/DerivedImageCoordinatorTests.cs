using System.Collections.Concurrent;
using Wharf.Image;

namespace Wharf.Tests;

public sealed class DerivedImageCoordinatorTests
{
    [Fact]
    public async Task GatesBaseContextAndBuildWithConfiguredConcurrency()
    {
        var backend = new RecordingBackend();
        await using var coordinator = new DerivedImageCoordinator(backend, 1);

        Task<DerivedImageResult>[] builds = Enumerable.Range(0, 3)
            .Select(index => coordinator.PrepareAsync(Request($"base:{index}")))
            .ToArray();
        await Task.WhenAll(builds);

        Assert.Equal(1, backend.MaximumActiveOperations);
        Assert.Equal(9, backend.Operations.Count);
    }

    [Fact]
    public async Task CoalescesIdenticalInFlightBuilds()
    {
        var backend = new RecordingBackend { BlockBuild = true };
        await using var coordinator = new DerivedImageCoordinator(backend, 2);
        Task<DerivedImageResult> first = coordinator.PrepareAsync(Request("ubuntu:24.04"));
        await backend.BuildStarted.Task;
        Task<DerivedImageResult> second = coordinator.PrepareAsync(Request("ubuntu:24.04"));

        backend.ReleaseBuild.TrySetResult();
        DerivedImageResult[] results = await Task.WhenAll(first, second);

        Assert.Equal(1, backend.BuildCount);
        Assert.Equal(results[0].CacheKey, results[1].CacheKey);
    }

    [Fact]
    public async Task ReusesSuccessfulBuildAndForceBuildBypassesCache()
    {
        var backend = new RecordingBackend();
        await using var coordinator = new DerivedImageCoordinator(backend, 2);
        DerivedImageResult first = await coordinator.PrepareAsync(Request("ubuntu:24.04"));
        DerivedImageResult reused = await coordinator.PrepareAsync(Request("ubuntu:24.04"));
        DerivedImageResult forced = await coordinator.PrepareAsync(Request("ubuntu:24.04") with { ForceBuild = true });

        Assert.False(first.Reused);
        Assert.True(reused.Reused);
        Assert.True(forced.Forced);
        Assert.Equal(2, backend.BuildCount);
    }

    [Fact]
    public async Task CancellationOnlyCancelsCallerAndDoesNotPoisonSharedBuild()
    {
        var backend = new RecordingBackend { BlockBuild = true };
        await using var coordinator = new DerivedImageCoordinator(backend, 1);
        using var cancellation = new CancellationTokenSource();
        Task<DerivedImageResult> first = coordinator.PrepareAsync(Request("ubuntu:24.04"), cancellation.Token);
        await backend.BuildStarted.Task;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);

        backend.ReleaseBuild.TrySetResult();
        DerivedImageResult result = await coordinator.PrepareAsync(Request("ubuntu:24.04"));
        Assert.Equal("wharf-agent", result.Image[..11]);
        Assert.Equal(1, backend.BuildCount);
    }

    [Fact]
    public async Task FailedBuildIsRemovedSoLaterAttemptCanRetry()
    {
        var backend = new RecordingBackend { FailNextBuild = true };
        await using var coordinator = new DerivedImageCoordinator(backend, 1);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.PrepareAsync(Request("ubuntu:24.04")));

        DerivedImageResult result = await coordinator.PrepareAsync(Request("ubuntu:24.04"));
        Assert.False(result.Reused);
        Assert.Equal(2, backend.BuildCount);
    }

    private static DerivedImageRequest Request(string baseImage) =>
        new(
            baseImage,
            ["setup.sh", "install.sh"],
            new Dictionary<string, string> { ["dotnet"] = "10.0.401" },
            new Dictionary<string, string> { ["mode"] = "agent" });

    private sealed class RecordingBackend : IDerivedImageBackend
    {
        private int active;
        private int maximumActive;

        public bool BlockBuild { get; init; }
        public bool FailNextBuild { get; set; }
        public TaskCompletionSource BuildStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseBuild { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentQueue<string> Operations { get; } = new();
        public int BuildCount { get; private set; }
        public int MaximumActiveOperations => maximumActive;

        public Task EnsureBaseImageAsync(string image, CancellationToken cancellationToken)
        {
            Enter("base");
            Exit();
            return Task.CompletedTask;
        }

        public Task PrepareContextAsync(
            DerivedImageRequest request,
            string cacheKey,
            CancellationToken cancellationToken)
        {
            Enter("context");
            Exit();
            return Task.CompletedTask;
        }

        public async Task<string> BuildAsync(
            DerivedImageRequest request,
            string cacheKey,
            CancellationToken cancellationToken)
        {
            Enter("build");
            BuildCount++;
            BuildStarted.TrySetResult();
            if (BlockBuild)
            {
                await ReleaseBuild.Task;
            }

            Exit();
            if (FailNextBuild)
            {
                FailNextBuild = false;
                throw new InvalidOperationException("build failed");
            }

            return $"wharf-agent:{cacheKey[..12]}";
        }

        private void Enter(string operation)
        {
            Operations.Enqueue(operation);
            int current = Interlocked.Increment(ref active);
            int observed;
            do
            {
                observed = maximumActive;
            } while (current > observed &&
                     Interlocked.CompareExchange(ref maximumActive, current, observed) != observed);
        }

        private void Exit() => Interlocked.Decrement(ref active);
    }
}
