using Wharf.Operations;
using Wharf.State;

namespace Wharf.Tests;

public sealed class OperationsTests
{
    [Fact]
    public void StatusReadDoesNotMutateEvents()
    {
        var run = new RunId("run-status");
        IReadOnlyList<StateEvent> events =
        [
            Event(run, 1, "e1", StateEventTypes.TrialStarted, StateStatuses.Running, "trial-1"),
            Event(run, 2, "e2", StateEventTypes.InfrastructureRetryStarted, StateStatuses.Running, "trial-1", "retry-1")
        ];

        RunStatusSnapshot snapshot = RunStatusService.ReadOnly(
            run,
            events,
            1,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow);

        Assert.Equal(1, snapshot.OccupiedConcurrency);
        Assert.Equal(1, snapshot.InfrastructureRetries);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task StopCleansOnlyEphemeralResourcesAfterGracefulTimeout()
    {
        var run = new FakeRun();
        var cleaner = new RecordingCleaner();
        var service = new GracefulStopService(run, cleaner);

        await service.StopAsync(
            new HashSet<string>(["container-1"], StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(1));

        Assert.True(run.Cancelled);
        Assert.Equal(["container-1"], cleaner.Removed);
    }

    [Fact]
    public async Task CompletionAndBlockedNotificationsAreDurable()
    {
        using TemporaryDirectory temporary = new();
        var sink = new FileRunNotificationSink(temporary.Path);
        var service = new NotificationService(sink);

        await service.CompletedAsync("run-1");
        await service.BlockedAsync("run-2", "host capacity");

        string[] lines = File.ReadAllLines(Path.Combine(temporary.Path, "notifications.jsonl"));
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"status\":\"completed\"", lines[0]);
        Assert.Contains("\"status\":\"blocked\"", lines[1]);
    }

    private static StateEvent Event(
        RunId run,
        long sequence,
        string id,
        string type,
        string status,
        string trial,
        string? retry = null) =>
        new(
            StateSchema.Version,
            new EventId(id),
            sequence,
            run,
            type,
            status,
            new TrialId(trial),
            InfrastructureRetryId: retry is null ? null : new InfrastructureRetryId(retry));

    private sealed class FakeRun : IGracefulRun
    {
        public bool Cancelled { get; private set; }

        public Task CancelAsync(CancellationToken cancellationToken)
        {
            Cancelled = true;
            return Task.CompletedTask;
        }

        public Task WaitForCompletionAsync(CancellationToken cancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private sealed class RecordingCleaner : IEphemeralResourceCleaner
    {
        public List<string> Removed { get; } = [];

        public Task RemoveAsync(
            IReadOnlySet<string> resourceIds,
            CancellationToken cancellationToken)
        {
            Removed.AddRange(resourceIds);
            return Task.CompletedTask;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"wharf-operations-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
