using System.Text.Json;
using Wharf.State;

namespace Wharf.Image;

public sealed class FileImagePullTelemetry : IImagePullTelemetry
{
    private readonly string eventsPath;
    private readonly string summaryPath;
    private readonly SemaphoreSlim writerLock = new(1, 1);
    private readonly Dictionary<string, ImagePullResult> images = new(StringComparer.Ordinal);
    private int totalRateLimits;
    private long cumulativeDelay;

    public FileImagePullTelemetry(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("telemetry directory must not be empty", nameof(directory));
        }

        Directory.CreateDirectory(directory);
        eventsPath = Path.Combine(directory, "image-pulls.jsonl");
        summaryPath = Path.Combine(directory, "image-pull-summary.json");
    }

    public async Task RecordRateLimitAsync(
        ImageRateLimitEvent rateLimitEvent,
        CancellationToken cancellationToken)
    {
        await writerLock.WaitAsync(cancellationToken);
        try
        {
            string line = JsonSerializer.Serialize(rateLimitEvent, StateJson.Options) + Environment.NewLine;
            await using var stream = new FileStream(
                eventsPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous);
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(line.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
            stream.Flush(true);
            totalRateLimits++;
            cumulativeDelay += rateLimitEvent.RetryDelayMilliseconds;
        }
        finally
        {
            writerLock.Release();
        }
    }

    public async Task RecordResultAsync(
        ImagePullResult result,
        CancellationToken cancellationToken)
    {
        await writerLock.WaitAsync(cancellationToken);
        try
        {
            images[result.Image] = result;
            var summary = new ImagePullSummary(
                ImagePullSchema.Version,
                totalRateLimits,
                images.Values.Count(item => item.RateLimitResponses > 0),
                images.Values.Count(item => item.Outcome == "exhausted"),
                cumulativeDelay,
                new Dictionary<string, ImagePullResult>(images, StringComparer.Ordinal));
            AtomicFile.Replace(summaryPath, JsonSerializer.Serialize(summary, StateJson.Options));
        }
        finally
        {
            writerLock.Release();
        }
    }
}
