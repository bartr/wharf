using System.Text.Json;
using Wharf.State;

namespace Wharf.Operations;

public sealed record RunStatusSnapshot(
    string RunId,
    string Status,
    IReadOnlyDictionary<string, int> LifecycleCounts,
    int OccupiedConcurrency,
    int InfrastructureRetries,
    TimeSpan? EstimatedRemaining,
    long LastSequence);

public sealed record RunNotification(
    string SchemaVersion,
    string RunId,
    string Status,
    string Message,
    DateTimeOffset RecordedAt);

public interface IRunNotificationSink
{
    Task NotifyAsync(RunNotification notification, CancellationToken cancellationToken);
}

public sealed class FileRunNotificationSink : IRunNotificationSink
{
    private readonly string path;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    public FileRunNotificationSink(string directory)
    {
        Directory.CreateDirectory(directory);
        path = Path.Combine(directory, "notifications.jsonl");
    }

    public async Task NotifyAsync(
        RunNotification notification,
        CancellationToken cancellationToken)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(
                path,
                JsonSerializer.Serialize(notification, StateJson.Options) + Environment.NewLine,
                cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }
}

public interface IEphemeralResourceCleaner
{
    Task RemoveAsync(IReadOnlySet<string> resourceIds, CancellationToken cancellationToken);
}

public interface IGracefulRun
{
    Task WaitForCompletionAsync(CancellationToken cancellationToken);
    Task CancelAsync(CancellationToken cancellationToken);
}

public sealed class OperationsException(string message) : Exception(message);
