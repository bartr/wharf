using System.Text.Json.Serialization;

namespace Wharf.Image;

public static class ImagePullSchema
{
    public const string Version = "1.0";
}

public sealed record ImagePullOptions(
    int MaximumAttempts = 5,
    TimeSpan? InitialRetryDelay = null,
    TimeSpan? MaximumRetryDelay = null,
    double JitterRatio = 0.2)
{
    public TimeSpan InitialDelay => InitialRetryDelay ?? TimeSpan.FromSeconds(1);
    public TimeSpan MaximumDelay => MaximumRetryDelay ?? TimeSpan.FromSeconds(30);

    public void Validate()
    {
        if (MaximumAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumAttempts), "maximum attempts must be positive");
        }

        if (InitialDelay <= TimeSpan.Zero || MaximumDelay < InitialDelay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(InitialRetryDelay),
                "retry delays must be positive and the maximum must not be less than the initial delay");
        }

        if (JitterRatio is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(JitterRatio), "jitter ratio must be between zero and one");
        }
    }
}

public sealed record ImagePullAttempt(bool Succeeded, string StandardOutput, string StandardError);

public sealed record ImagePullResult(
    string Image,
    string Registry,
    string Outcome,
    int PullAttempts,
    int RateLimitResponses,
    long CumulativeRetryDelayMilliseconds,
    string? Error = null);

public sealed record ImageRateLimitEvent(
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
    string Registry,
    string Image,
    int Attempt,
    long RetryDelayMilliseconds,
    string Outcome);

public sealed record ImagePullSummary(
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
    int TotalRateLimitResponses,
    int AffectedImages,
    int ExhaustedRetries,
    long CumulativeRetryDelayMilliseconds,
    IReadOnlyDictionary<string, ImagePullResult> Images);

public interface IImageClient
{
    Task<bool> ExistsAsync(string image, CancellationToken cancellationToken);
    Task<ImagePullAttempt> PullAsync(string image, CancellationToken cancellationToken);
}

public interface IAsyncDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public interface IRandomSource
{
    double NextDouble();
}

public interface IImagePullTelemetry
{
    Task RecordRateLimitAsync(ImageRateLimitEvent rateLimitEvent, CancellationToken cancellationToken);
    Task RecordResultAsync(ImagePullResult result, CancellationToken cancellationToken);
}

public interface IImagePullConsole
{
    void RateLimitRetry(ImageRateLimitEvent rateLimitEvent);
    void RateLimitSummary(ImagePullResult result);
}

public sealed class ImagePullException(ImagePullResult result)
    : Exception($"image pull failed for {result.Image}: {result.Error}")
{
    public ImagePullResult Result { get; } = result;
}

public sealed class DockerImageException(string message) : Exception(message);

public sealed class SystemAsyncDelay : IAsyncDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, cancellationToken);
}

public sealed class SystemRandomSource : IRandomSource
{
    public double NextDouble() => Random.Shared.NextDouble();
}

public sealed class NullImagePullTelemetry : IImagePullTelemetry
{
    public Task RecordRateLimitAsync(
        ImageRateLimitEvent rateLimitEvent,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RecordResultAsync(
        ImagePullResult result,
        CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class ImagePullConsole(int verbosity, bool quiet = false) : IImagePullConsole
{
    public void RateLimitRetry(ImageRateLimitEvent rateLimitEvent)
    {
        if (!quiet && verbosity >= 2)
        {
            Console.Error.WriteLine(
                $"WARN\tregistry 429\timage={rateLimitEvent.Image}\tattempt={rateLimitEvent.Attempt}\tdelayMs={rateLimitEvent.RetryDelayMilliseconds}\toutcome={rateLimitEvent.Outcome}");
        }
    }

    public void RateLimitSummary(ImagePullResult result)
    {
        if (!quiet)
        {
            Console.Error.WriteLine(
                $"WARN\tregistry rate limit\timage={result.Image}\tresponses={result.RateLimitResponses}\tdelayMs={result.CumulativeRetryDelayMilliseconds}\toutcome={result.Outcome}");
        }
    }
}
