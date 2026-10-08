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
                _ => throw new InvalidOperationException("failed"),
                CancellationToken.None));

        int result = await gate.ExecuteAsync(
            _ => Task.FromResult(42),
            CancellationToken.None);
        Assert.Equal(42, result);
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
