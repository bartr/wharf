using System.Text.Json;
using Wharf.State;

namespace Wharf.Operations;

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

public sealed class OperationsException(string message) : Exception(message);
