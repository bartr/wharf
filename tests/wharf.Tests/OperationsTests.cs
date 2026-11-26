using Wharf.Operations;

namespace Wharf.Tests;

public sealed class OperationsTests
{
    [Fact]
    public async Task CompletionAndBlockedNotificationsAreDurable()
    {
        using TemporaryDirectory temporary = new();
        var sink = new FileRunNotificationSink(temporary.Path);
        var service = new NotificationService(sink);

        await service.CompletedAsync("run-1");
        await service.BlockedAsync("run-2", "host capacity");

        string[] lines = File.ReadAllLines(Path.Combine(temporary.Path, "notifications.jsonl"));
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"status\":\"completed\"", lines[0]);
        Assert.Contains("\"status\":\"blocked\"", lines[1]);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"wharf-operations-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
