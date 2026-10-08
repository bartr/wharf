namespace Wharf.Run;

internal sealed class PreparationGate : IDisposable, IAsyncDisposable
{
    private readonly SemaphoreSlim gate;

    public PreparationGate(int concurrency)
    {
        if (concurrency < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(concurrency),
                "preparation concurrency must be positive");
        }

        gate = new SemaphoreSlim(concurrency, concurrency);
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await operation(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose() => gate.Dispose();
}
