using Wharf.Execution;

namespace Wharf.Tests;

public sealed class PhaseExecutionTests
{
    [Fact]
    public async Task EnforcesPhaseConcurrency()
    {
        int active = 0;
        int maximum = 0;
        await using var service = new PhaseExecutionService(
            new PhaseResourceLimits(1, 2, 1),
            Timeouts(),
            new InfrastructureRetryPolicy(0));

        Task<PhaseExecutionResult<int>>[] work = Enumerable.Range(0, 6)
            .Select(_ => service.ExecuteAsync(
                Guid.NewGuid().ToString(),
                ExecutionPhase.Agent,
                async _ =>
                {
                    int current = Interlocked.Increment(ref active);
                    InterlockedMax(ref maximum, current);
                    await Task.Delay(5);
                    Interlocked.Decrement(ref active);
                    return 1;
                },
                _ => false))
            .ToArray();

        await Task.WhenAll(work);
        Assert.Equal(2, maximum);
    }

    [Fact]
    public async Task RetriesInfrastructureFailureWithoutChangingLogicalAttempt()
    {
        var sink = new RecordingSink();
        int calls = 0;
        await using var service = new PhaseExecutionService(
            new PhaseResourceLimits(1, 1, 1),
            Timeouts(),
            new InfrastructureRetryPolicy(2),
            sink);

        PhaseExecutionResult<string> result = await service.ExecuteAsync(
            "logical-1",
            ExecutionPhase.Build,
            _ =>
            {
                calls++;
                return Task.FromResult(calls == 1
                    ? throw new InvalidOperationException("daemon unavailable")
                    : "built");
            },
            exception => exception is InvalidOperationException);

        Assert.Equal("built", result.Value);
        Assert.Equal(1, result.InfrastructureRetries);
        Assert.Single(sink.Evidence);
        Assert.Equal("logical-1", sink.Evidence[0].LogicalAttemptId);
    }

    [Fact]
    public async Task AppliesTimeoutMultiplierAndRetriesTimeout()
    {
        var sink = new RecordingSink();
        int calls = 0;
        await using var service = new PhaseExecutionService(
            new PhaseResourceLimits(1, 1, 1),
            new PhaseTimeoutPolicy(
                TimeSpan.FromMilliseconds(5),
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1),
                Multiplier: 1,
                Overrides: new Dictionary<ExecutionPhase, TimeSpan>
                {
                    [ExecutionPhase.Build] = TimeSpan.FromMilliseconds(5)
                }),
            new InfrastructureRetryPolicy(1),
            sink);

        PhaseExecutionResult<string> result = await service.ExecuteAsync(
            "logical-timeout",
            ExecutionPhase.Build,
            async token =>
            {
                calls++;
                if (calls == 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), token);
                }

                return "built";
            },
            exception => exception is TimeoutException);

        Assert.Equal("built", result.Value);
        Assert.True(result.InfrastructureRetries == 1);
        Assert.Equal("timeout", sink.Evidence[0].Classification);
    }

    [Fact]
    public async Task DoesNotRetryExcludedFailure()
    {
        await using var service = new PhaseExecutionService(
            new PhaseResourceLimits(1, 1, 1),
            Timeouts(),
            new InfrastructureRetryPolicy(3));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExecuteAsync<string>(
                "logical-failure",
                ExecutionPhase.Verifier,
                _ => throw new InvalidOperationException("test failed"),
                _ => false));
    }

    private static PhaseTimeoutPolicy Timeouts() =>
        new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

    private static void InterlockedMax(ref int location, int value)
    {
        int current;
        do
        {
            current = location;
        } while (value > current &&
                 Interlocked.CompareExchange(ref location, value, current) != current);
    }

    private sealed class RecordingSink : IInfrastructureRetrySink
    {
        public List<InfrastructureRetryEvidence> Evidence { get; } = [];

        public Task RecordAsync(
            InfrastructureRetryEvidence evidence,
            CancellationToken cancellationToken)
        {
            Evidence.Add(evidence);
            return Task.CompletedTask;
        }
    }
}
