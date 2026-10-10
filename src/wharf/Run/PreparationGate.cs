using System.Diagnostics;

namespace Wharf.Run;

internal enum PreparationOperation
{
    Agent,
    AgentRetry,
    Verifier
}

internal sealed record PreparationGateContext(
    int RunNumber,
    string TaskId,
    int LogicalAttempt,
    int TrialRetry,
    int K,
    int OperationRetry = 0);

internal sealed record PreparationGateEvent(
    long Sequence,
    PreparationOperation Operation,
    PreparationGateContext Context,
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
        PreparationGateContext context,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        PreparationGateEvent waitingEvent;
        lock (sync)
        {
            waiting++;
            waitingEvent = Snapshot(operation, context, "waiting", 0);
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
                cancelledEvent = Snapshot(
                    operation,
                    context,
                    "cancelled",
                    Math.Max(0, stopwatch.ElapsedMilliseconds));
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
                context,
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
                releasedEvent = Snapshot(operation, context, "released");
                gate.Release();
            }
            telemetry?.Invoke(releasedEvent);
        }
    }

    private PreparationGateEvent Snapshot(
        PreparationOperation operation,
        PreparationGateContext context,
        string transition,
        long? waitMilliseconds = null) =>
        new(
            ++sequence,
            operation,
            context,
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
