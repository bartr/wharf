using System.Text.Json;
using Wharf.State;

namespace Wharf.Execution;

public enum ExecutionPhase
{
    Build,
    Agent,
    Verifier
}

public sealed record PhaseResourceLimits(int Build, int Agent, int Verifier)
{
    public int For(ExecutionPhase phase) => phase switch
    {
        ExecutionPhase.Build => Build,
        ExecutionPhase.Agent => Agent,
        ExecutionPhase.Verifier => Verifier,
        _ => throw new ArgumentOutOfRangeException(nameof(phase))
    };

    public void Validate()
    {
        if (Build < 1 || Agent < 1 || Verifier < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Build), "phase resource limits must be positive");
        }
    }
}

public sealed record PhaseTimeoutPolicy(
    TimeSpan Build,
    TimeSpan Agent,
    TimeSpan Verifier,
    double Multiplier = 1,
    IReadOnlyDictionary<ExecutionPhase, TimeSpan>? Overrides = null)
{
    public TimeSpan For(ExecutionPhase phase)
    {
        TimeSpan baseTimeout = phase switch
        {
            ExecutionPhase.Build => Build,
            ExecutionPhase.Agent => Agent,
            ExecutionPhase.Verifier => Verifier,
            _ => throw new ArgumentOutOfRangeException(nameof(phase))
        };
        TimeSpan selected = Overrides is not null && Overrides.TryGetValue(phase, out TimeSpan overrideTimeout)
            ? overrideTimeout
            : baseTimeout;
        return TimeSpan.FromMilliseconds(selected.TotalMilliseconds * Multiplier);
    }

    public void Validate()
    {
        if (Build <= TimeSpan.Zero || Agent <= TimeSpan.Zero || Verifier <= TimeSpan.Zero ||
            Multiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Multiplier), "phase timeouts must be positive");
        }
    }
}

public sealed record InfrastructureRetryPolicy(int MaximumRetries)
{
    public void Validate()
    {
        if (MaximumRetries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumRetries), "maximum retries cannot be negative");
        }
    }
}

public sealed record InfrastructureRetryEvidence(
    [property: System.Text.Json.Serialization.JsonPropertyName("schemaVersion")] string SchemaVersion,
    string InfrastructureRetryId,
    string LogicalAttemptId,
    ExecutionPhase Phase,
    int RetryNumber,
    string Classification,
    string Outcome,
    string Error);

public sealed record PhaseExecutionResult<T>(
    T Value,
    int InfrastructureRetries,
    bool TimedOut);

public interface IInfrastructureRetrySink
{
    Task RecordAsync(InfrastructureRetryEvidence evidence, CancellationToken cancellationToken);
}

public sealed class NullInfrastructureRetrySink : IInfrastructureRetrySink
{
    public Task RecordAsync(
        InfrastructureRetryEvidence evidence,
        CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class FileInfrastructureRetrySink : IInfrastructureRetrySink
{
    private readonly string path;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    public FileInfrastructureRetrySink(string directory)
    {
        Directory.CreateDirectory(directory);
        path = Path.Combine(directory, "infrastructure-retries.jsonl");
    }

    public async Task RecordAsync(
        InfrastructureRetryEvidence evidence,
        CancellationToken cancellationToken)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(
                path,
                JsonSerializer.Serialize(evidence, StateJson.Options) + Environment.NewLine,
                cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }
}

public sealed class PhaseExecutionException(string message, Exception innerException)
    : Exception(message, innerException);
