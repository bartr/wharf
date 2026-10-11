using System.Collections.Concurrent;
using System.Text.Json;
using Wharf.Image;

namespace Wharf.Tests;

public sealed class ImagePullCoordinatorTests
{
    [Fact]
    public async Task ReusesLocalImageWithoutPulling()
    {
        var client = new FakeImageClient(exists: true, Array.Empty<ImagePullAttempt>());
        var telemetry = new RecordingTelemetry();
        ImagePullCoordinator coordinator = CreateCoordinator(client, telemetry: telemetry);

        ImagePullResult result = await coordinator.PrepareAsync("ubuntu:24.04");

        Assert.Equal("local", result.Outcome);
        Assert.Equal(0, client.PullCount);
        Assert.Empty(telemetry.RateLimits);
        Assert.Single(telemetry.Results);
    }

    [Fact]
    public async Task CoalescesConcurrentDuplicatePulls()
    {
        var releasePull = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new FakeImageClient(
            exists: false,
            async cancellationToken =>
            {
                await releasePull.Task.WaitAsync(cancellationToken);
                return Success();
            });
        ImagePullCoordinator coordinator = CreateCoordinator(client);

        Task<ImagePullResult>[] pulls = Enumerable.Range(0, 20)
            .Select(_ => coordinator.PrepareAsync("registry.example.test/team/base:1"))
            .ToArray();
        await client.PullStarted.Task;
        Assert.Equal(1, client.PullCount);

        releasePull.SetResult();
        ImagePullResult[] results = await Task.WhenAll(pulls);

        Assert.All(results, result => Assert.Equal("pulled", result.Outcome));
        Assert.Equal(1, client.PullCount);
    }

    [Fact]
    public async Task RetriesRateLimitsWithIncreasingBoundedDelayAndCompleteEvidence()
    {
        var client = new FakeImageClient(
            false,
            RateLimited("received unexpected HTTP status: 429 Too Many Requests"),
            RateLimited("toomanyrequests: rate limit exceeded"),
            Success());
        var delay = new RecordingDelay();
        var telemetry = new RecordingTelemetry();
        var console = new RecordingConsole();
        ImagePullCoordinator coordinator = CreateCoordinator(
            client,
            delay,
            telemetry,
            console,
            new ImagePullOptions(
                MaximumAttempts: 4,
                InitialRetryDelay: TimeSpan.FromMilliseconds(100),
                MaximumRetryDelay: TimeSpan.FromMilliseconds(150),
                JitterRatio: 0));

        ImagePullResult result = await coordinator.PrepareAsync("ghcr.io/acme/base:1");

        Assert.Equal("pulled", result.Outcome);
        Assert.Equal(3, result.PullAttempts);
        Assert.Equal(2, result.RateLimitResponses);
        Assert.Equal(250, result.CumulativeRetryDelayMilliseconds);
        Assert.Equal([100, 150], delay.Delays.Select(item => (long)item.TotalMilliseconds));
        Assert.Equal(2, telemetry.RateLimits.Count);
        Assert.All(telemetry.RateLimits, item => Assert.Equal("ghcr.io", item.Registry));
        Assert.All(telemetry.RateLimits, item => Assert.Equal("retrying", item.Outcome));
        Assert.Equal(2, console.Retries.Count);
        Assert.Single(console.Summaries);
    }

    [Fact]
    public async Task SuccessDecaysRegistryPressureForLaterPulls()
    {
        var client = new FakeImageClient(
            false,
            RateLimited("429"),
            Success(),
            RateLimited("429"),
            Success());
        var delay = new RecordingDelay();
        ImagePullCoordinator coordinator = CreateCoordinator(
            client,
            delay,
            options: new ImagePullOptions(
                InitialRetryDelay: TimeSpan.FromMilliseconds(100),
                MaximumRetryDelay: TimeSpan.FromSeconds(1),
                JitterRatio: 0));

        await coordinator.PrepareAsync("ghcr.io/acme/first:1");
        await coordinator.PrepareAsync("ghcr.io/acme/second:1");

        Assert.Equal([100, 100], delay.Delays.Select(item => (long)item.TotalMilliseconds));
    }

    [Theory]
    [InlineData(0, 80)]
    [InlineData(1, 120)]
    public async Task JitterUsesInjectedRandomSourceWithinConfiguredBounds(
        double randomValue,
        long expectedDelay)
    {
        var client = new FakeImageClient(false, RateLimited("429"), Success());
        var delay = new RecordingDelay();
        var coordinator = new ImagePullCoordinator(
            client,
            delay,
            new FixedRandomSource(randomValue),
            new RecordingTelemetry(),
            new RecordingConsole(),
            new ImagePullOptions(
                InitialRetryDelay: TimeSpan.FromMilliseconds(100),
                MaximumRetryDelay: TimeSpan.FromMilliseconds(200),
                JitterRatio: 0.2));

        await coordinator.PrepareAsync("ubuntu:24.04");

        Assert.Equal(expectedDelay, (long)Assert.Single(delay.Delays).TotalMilliseconds);
    }

    [Fact]
    public async Task DeterministicFailureIsNotRetried()
    {
        var client = new FakeImageClient(false, Failed("manifest unknown"));
        var delay = new RecordingDelay();
        var telemetry = new RecordingTelemetry();
        ImagePullCoordinator coordinator = CreateCoordinator(client, delay, telemetry);

        ImagePullException exception = await Assert.ThrowsAsync<ImagePullException>(
            () => coordinator.PrepareAsync("ubuntu:does-not-exist"));

        Assert.Equal("failed", exception.Result.Outcome);
        Assert.Equal(1, client.PullCount);
        Assert.Empty(delay.Delays);
        Assert.Empty(telemetry.RateLimits);
        Assert.Single(telemetry.Results);
    }

    [Fact]
    public async Task ExhaustionPersistsEvery429AndAggregateSummary()
    {
        using TemporaryDirectory temporary = new();
        var client = new FakeImageClient(false, RateLimited("429"), RateLimited("429"));
        var telemetry = new FileImagePullTelemetry(temporary.Path);
        ImagePullCoordinator coordinator = CreateCoordinator(
            client,
            telemetry: telemetry,
            options: new ImagePullOptions(
                MaximumAttempts: 2,
                InitialRetryDelay: TimeSpan.FromMilliseconds(25),
                MaximumRetryDelay: TimeSpan.FromMilliseconds(100),
                JitterRatio: 0));

        ImagePullException exception = await Assert.ThrowsAsync<ImagePullException>(
            () => coordinator.PrepareAsync("registry.example.test/base:1"));

        Assert.Equal("exhausted", exception.Result.Outcome);
        string[] events = await File.ReadAllLinesAsync(
            Path.Combine(temporary.Path, "image-pulls.jsonl"));
        Assert.Equal(2, events.Length);
        Assert.Contains("\"outcome\":\"retrying\"", events[0]);
        Assert.Contains("\"outcome\":\"exhausted\"", events[1]);
        ImagePullSummary summary = JsonSerializer.Deserialize<ImagePullSummary>(
            await File.ReadAllTextAsync(Path.Combine(temporary.Path, "image-pull-summary.json")),
            Wharf.State.StateJson.Options)!;
        Assert.Equal(ImagePullSchema.Version, summary.SchemaVersion);
        Assert.Equal(2, summary.TotalRateLimitResponses);
        Assert.Equal(1, summary.AffectedImages);
        Assert.Equal(1, summary.ExhaustedRetries);
        Assert.Equal(25, summary.CumulativeRetryDelayMilliseconds);
        Assert.Equal("exhausted", summary.Images["registry.example.test/base:1"].Outcome);
    }

    [Theory]
    [InlineData("ubuntu:24.04", "docker.io")]
    [InlineData("library/ubuntu:24.04", "docker.io")]
    [InlineData("localhost:5000/base:1", "localhost:5000")]
    [InlineData("ghcr.io/acme/base@sha256:abc", "ghcr.io")]
    public void IdentifiesRegistry(string image, string expected)
    {
        Assert.Equal(expected, ImagePullCoordinator.GetRegistry(image));
    }

    private static ImagePullCoordinator CreateCoordinator(
        IImageClient client,
        IAsyncDelay? delay = null,
        IImagePullTelemetry? telemetry = null,
        IImagePullConsole? console = null,
        ImagePullOptions? options = null) =>
        new(
            client,
            delay ?? new RecordingDelay(),
            new FixedRandomSource(0.5),
            telemetry ?? new RecordingTelemetry(),
            console ?? new RecordingConsole(),
            options);

    private static ImagePullAttempt Success() => new(true, "pulled", string.Empty);
    private static ImagePullAttempt RateLimited(string error) => new(false, string.Empty, error);
    private static ImagePullAttempt Failed(string error) => new(false, string.Empty, error);

    private sealed class FakeImageClient : IImageClient
    {
        private readonly bool exists;
        private readonly ConcurrentQueue<Func<CancellationToken, Task<ImagePullAttempt>>> attempts = new();
        private int pullCount;

        public FakeImageClient(bool exists, params ImagePullAttempt[] attempts)
            : this(exists, attempts.Select<ImagePullAttempt, Func<CancellationToken, Task<ImagePullAttempt>>>(
                item => _ => Task.FromResult(item)).ToArray())
        {
        }

        public FakeImageClient(
            bool exists,
            params Func<CancellationToken, Task<ImagePullAttempt>>[] attempts)
        {
            this.exists = exists;
            foreach (Func<CancellationToken, Task<ImagePullAttempt>> attempt in attempts)
            {
                this.attempts.Enqueue(attempt);
            }
        }

        public int PullCount => pullCount;
        public TaskCompletionSource PullStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<bool> ExistsAsync(string image, CancellationToken cancellationToken) =>
            Task.FromResult(exists);

        public Task<ImagePullAttempt> PullAsync(string image, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref pullCount);
            PullStarted.TrySetResult();
            if (!attempts.TryDequeue(out Func<CancellationToken, Task<ImagePullAttempt>>? attempt))
            {
                throw new InvalidOperationException("No fake pull attempt is available");
            }

            return attempt(cancellationToken);
        }
    }

    private sealed class RecordingDelay : IAsyncDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedRandomSource(double value) : IRandomSource
    {
        public double NextDouble() => value;
    }

    private sealed class RecordingTelemetry : IImagePullTelemetry
    {
        public List<ImageRateLimitEvent> RateLimits { get; } = [];
        public List<ImagePullResult> Results { get; } = [];

        public Task RecordRateLimitAsync(
            ImageRateLimitEvent rateLimitEvent,
            CancellationToken cancellationToken)
        {
            RateLimits.Add(rateLimitEvent);
            return Task.CompletedTask;
        }

        public Task RecordResultAsync(
            ImagePullResult result,
            CancellationToken cancellationToken)
        {
            Results.Add(result);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingConsole : IImagePullConsole
    {
        public List<ImageRateLimitEvent> Retries { get; } = [];
        public List<ImagePullResult> Summaries { get; } = [];

        public void RateLimitRetry(ImageRateLimitEvent rateLimitEvent) => Retries.Add(rateLimitEvent);
        public void RateLimitSummary(ImagePullResult result) => Summaries.Add(result);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"wharf-images-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
