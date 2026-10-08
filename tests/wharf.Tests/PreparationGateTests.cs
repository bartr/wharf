using Wharf.Run;

namespace Wharf.Tests;

public sealed class PreparationGateTests
{
    [Fact]
    public async Task LimitsPreparationWithoutLimitingWaitingTrials()
    {
        int active = 0;
        int maximum = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var gate = new PreparationGate(2);

        Task<int>[] work = Enumerable.Range(0, 6)
            .Select(index => gate.ExecuteAsync(
                PreparationOperation.Agent,
                async cancellationToken =>
                {
                    int current = Interlocked.Increment(ref active);
                    InterlockedMax(ref maximum, current);
                    await release.Task.WaitAsync(cancellationToken);
                    Interlocked.Decrement(ref active);
                    return index;
                },
                CancellationToken.None))
            .ToArray();

        await WaitUntilAsync(() => Volatile.Read(ref active) == 2);
        Assert.Equal(2, maximum);
        Assert.Equal(6, work.Length);

        release.SetResult();
        await Task.WhenAll(work);
        Assert.Equal(2, maximum);
    }

    [Fact]
    public async Task ReleasesPermitAfterFailure()
    {
        await using var gate = new PreparationGate(1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => gate.ExecuteAsync<int>(
                PreparationOperation.Agent,
                _ => throw new InvalidOperationException("failed"),
                CancellationToken.None));

        int result = await gate.ExecuteAsync(
            PreparationOperation.Verifier,
            _ => Task.FromResult(42),
            CancellationToken.None);
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ReportsOrderedOccupancyWaitAndReleaseEvents()
    {
        var events = new List<PreparationGateEvent>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var gate = new PreparationGate(1, gateEvent =>
        {
            lock (events)
                events.Add(gateEvent);
        });

        Task<int> first = gate.ExecuteAsync(
            PreparationOperation.Agent,
            async cancellationToken =>
            {
                await release.Task.WaitAsync(cancellationToken);
                return 1;
            },
            CancellationToken.None);
        await WaitUntilAsync(() =>
        {
            lock (events)
                return events.Any(gateEvent => gateEvent.Transition == "acquired");
        });
        Task<int> second = gate.ExecuteAsync(
            PreparationOperation.Verifier,
            _ => Task.FromResult(2),
            CancellationToken.None);
        await WaitUntilAsync(() =>
        {
            lock (events)
                return events.Any(gateEvent =>
                    gateEvent.Operation == PreparationOperation.Verifier &&
                    gateEvent.Transition == "waiting");
        });

        release.SetResult();
        int[] results = await Task.WhenAll(first, second);
        Assert.Equal([1, 2], results);

        PreparationGateEvent[] snapshots;
        lock (events)
            snapshots = events.OrderBy(gateEvent => gateEvent.Sequence).ToArray();
        Assert.Equal(
            Enumerable.Range(1, snapshots.Length).Select(value => (long)value),
            snapshots.Select(gateEvent => gateEvent.Sequence));
        Assert.All(snapshots, gateEvent =>
        {
            Assert.InRange(gateEvent.Active, 0, gateEvent.Capacity);
            Assert.True(gateEvent.Waiting >= 0);
        });
        Assert.Contains(snapshots, gateEvent =>
            gateEvent.Operation == PreparationOperation.Verifier &&
            gateEvent.Transition == "acquired" &&
            gateEvent.WaitMilliseconds >= 0);
        Assert.Equal(0, snapshots[^1].Active);
        Assert.Equal(0, snapshots[^1].Waiting);
    }

    [Fact]
    public async Task ReportsCancelledWaitWithoutLeakingOccupancy()
    {
        var events = new List<PreparationGateEvent>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var gate = new PreparationGate(1, gateEvent =>
        {
            lock (events)
                events.Add(gateEvent);
        });
        Task<int> active = gate.ExecuteAsync(
            PreparationOperation.Agent,
            async cancellationToken =>
            {
                await release.Task.WaitAsync(cancellationToken);
                return 1;
            },
            CancellationToken.None);
        await WaitUntilAsync(() =>
        {
            lock (events)
                return events.Any(gateEvent => gateEvent.Transition == "acquired");
        });
        using var cancellation = new CancellationTokenSource();
        Task<int> waiting = gate.ExecuteAsync(
            PreparationOperation.AgentRetry,
            _ => Task.FromResult(2),
            cancellation.Token);
        await WaitUntilAsync(() =>
        {
            lock (events)
                return events.Any(gateEvent =>
                    gateEvent.Operation == PreparationOperation.AgentRetry &&
                    gateEvent.Transition == "waiting");
        });

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        release.SetResult();
        await active;

        lock (events)
        {
            Assert.Contains(events, gateEvent =>
                gateEvent.Operation == PreparationOperation.AgentRetry &&
                gateEvent.Transition == "cancelled");
            Assert.Equal(0, events[^1].Active);
            Assert.Equal(0, events[^1].Waiting);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static void InterlockedMax(ref int location, int value)
    {
        int current;
        do
        {
            current = location;
        } while (value > current &&
                 Interlocked.CompareExchange(ref location, value, current) != current);
    }
}
