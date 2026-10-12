namespace Wharf.Execution;

public sealed class PhaseExecutionService : IAsyncDisposable
{
    private readonly PhaseResourceLimits resources;
    private readonly PhaseTimeoutPolicy timeouts;
    private readonly InfrastructureRetryPolicy retries;
    private readonly IInfrastructureRetrySink retrySink;
    private readonly Dictionary<ExecutionPhase, SemaphoreSlim> gates;
    private bool disposed;

    public PhaseExecutionService(
        PhaseResourceLimits resources,
        PhaseTimeoutPolicy timeouts,
        InfrastructureRetryPolicy retries,
        IInfrastructureRetrySink? retrySink = null)
    {
        resources.Validate();
        timeouts.Validate();
        retries.Validate();
        this.resources = resources;
        this.timeouts = timeouts;
        this.retries = retries;
        this.retrySink = retrySink ?? new NullInfrastructureRetrySink();
        gates = Enum.GetValues<ExecutionPhase>()
            .ToDictionary(phase => phase, phase => new SemaphoreSlim(resources.For(phase)));
    }

    public async Task<PhaseExecutionResult<T>> ExecuteAsync<T>(
        string logicalAttemptId,
        ExecutionPhase phase,
        Func<CancellationToken, Task<T>> operation,
        Func<Exception, bool> isRetryableInfrastructureFailure,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (string.IsNullOrWhiteSpace(logicalAttemptId))
        {
            throw new ArgumentException("logical attempt ID must not be empty", nameof(logicalAttemptId));
        }

        int infrastructureRetries = 0;
        while (true)
        {
            await gates[phase].WaitAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(timeouts.For(phase));
            try
            {
                T value = await operation(timeout.Token);
                return new PhaseExecutionResult<T>(value, infrastructureRetries, false);
            }
            catch (OperationCanceledException) when (
                !cancellationToken.IsCancellationRequested &&
                timeout.IsCancellationRequested)
            {
                var timeoutException = new TimeoutException($"{phase} phase timed out");
                if (!isRetryableInfrastructureFailure(timeoutException) ||
                    infrastructureRetries >= retries.MaximumRetries)
                {
                    throw new PhaseExecutionException(
                        $"{phase} phase exceeded its timeout",
                        timeoutException);
                }

                infrastructureRetries++;
                await RecordRetryAsync(
                    logicalAttemptId,
                    phase,
                    infrastructureRetries,
                    "timeout",
                    timeoutException,
                    cancellationToken);
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException &&
                isRetryableInfrastructureFailure(exception))
            {
                if (infrastructureRetries >= retries.MaximumRetries)
                {
                    throw new PhaseExecutionException(
                        $"{phase} phase exhausted infrastructure retries",
                        exception);
                }

                infrastructureRetries++;
                await RecordRetryAsync(
                    logicalAttemptId,
                    phase,
                    infrastructureRetries,
                    "retryable",
                    exception,
                    cancellationToken);
            }
            finally
            {
                gates[phase].Release();
            }
        }
    }

    private Task RecordRetryAsync(
        string logicalAttemptId,
        ExecutionPhase phase,
        int retryNumber,
        string classification,
        Exception exception,
        CancellationToken cancellationToken) =>
        retrySink.RecordAsync(
            new InfrastructureRetryEvidence(
                "1.0",
                Guid.NewGuid().ToString("N"),
                logicalAttemptId,
                phase,
                retryNumber,
                classification,
                "retrying",
                exception.Message),
            cancellationToken);

    public ValueTask DisposeAsync()
    {
        if (!disposed)
        {
            disposed = true;
            foreach (SemaphoreSlim gate in gates.Values)
            {
                gate.Dispose();
            }
        }

        return ValueTask.CompletedTask;
    }
}
