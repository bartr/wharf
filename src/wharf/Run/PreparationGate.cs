using System.Diagnostics;

namespace Wharf.Run;

internal enum PreparationOperation
{
    Agent,
    AgentRetry,
    Verifier
}

internal sealed record PreparationGateEvent(
    long Sequence,
    PreparationOperation Operation,
    string Transition,
    int Capacity,
    int Active,
    int Waiting,
    long? WaitMilliseconds = null);

internal sealed class PreparationGate : IDisposable, IAsyncDisposable
{
    private readonly SemaphoreSlim gate;
    private readonly int capacity;
    private readonly Action<PreparationGateEvent>? telemetry;
    private readonly object sync = new();
    private long sequence;
    private int active;
    private int waiting;

    public PreparationGate(
        int concurrency,
        Action<PreparationGateEvent>? telemetry = null)
    {
        if (concurrency < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(concurrency),
                "preparation concurrency must be positive");
        }

        capacity = concurrency;
        this.telemetry = telemetry;
        gate = new SemaphoreSlim(concurrency, concurrency);
    }

    public async Task<T> ExecuteAsync<T>(
        PreparationOperation operation,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        PreparationGateEvent waitingEvent;
        lock (sync)
        {
            waiting++;
            waitingEvent = Snapshot(operation, "waiting");
        }
        try
        {
            telemetry?.Invoke(waitingEvent);
        }
        catch
        {
            lock (sync)
                waiting--;
            throw;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await gate.WaitAsync(cancellationToken);
        }
        catch
        {
            PreparationGateEvent cancelledEvent;
            lock (sync)
            {
                waiting--;
                cancelledEvent = Snapshot(operation, "cancelled");
            }
            telemetry?.Invoke(cancelledEvent);
            throw;
        }

        PreparationGateEvent acquiredEvent;
        lock (sync)
        {
            waiting--;
            active++;
            acquiredEvent = Snapshot(
                operation,
                "acquired",
                Math.Max(0, stopwatch.ElapsedMilliseconds));
        }
        try
        {
            telemetry?.Invoke(acquiredEvent);
            return await action(cancellationToken);
        }
        finally
        {
            PreparationGateEvent releasedEvent;
            lock (sync)
            {
                active--;
                releasedEvent = Snapshot(operation, "released");
                gate.Release();
            }
            telemetry?.Invoke(releasedEvent);
        }
    }

    private PreparationGateEvent Snapshot(
        PreparationOperation operation,
        string transition,
        long? waitMilliseconds = null) =>
        new(
            ++sequence,
            operation,
            transition,
            capacity,
            active,
            waiting,
            waitMilliseconds);

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose() => gate.Dispose();
}
