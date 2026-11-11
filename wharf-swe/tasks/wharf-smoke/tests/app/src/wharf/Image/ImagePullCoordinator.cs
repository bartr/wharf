using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Wharf.Image;

public sealed partial class ImagePullCoordinator
{
    private readonly IImageClient client;
    private readonly IAsyncDelay delay;
    private readonly IRandomSource random;
    private readonly IImagePullTelemetry telemetry;
    private readonly IImagePullConsole console;
    private readonly ImagePullOptions options;
    private readonly ConcurrentDictionary<string, Lazy<Task<ImagePullResult>>> pulls =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> registryPressure =
        new(StringComparer.OrdinalIgnoreCase);

    public ImagePullCoordinator(
        IImageClient client,
        IAsyncDelay delay,
        IRandomSource random,
        IImagePullTelemetry telemetry,
        IImagePullConsole console,
        ImagePullOptions? options = null)
    {
        this.client = client;
        this.delay = delay;
        this.random = random;
        this.telemetry = telemetry;
        this.console = console;
        this.options = options ?? new ImagePullOptions();
        this.options.Validate();
    }

    public Task<ImagePullResult> PrepareAsync(
        string image,
        CancellationToken cancellationToken = default)
    {
        string normalized = NormalizeImage(image);
        Lazy<Task<ImagePullResult>> pending = pulls.GetOrAdd(
            normalized,
            key => new Lazy<Task<ImagePullResult>>(
                () => PrepareCoreAsync(key, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));
        return pending.Value;
    }

    public static bool IsRateLimitFailure(ImagePullAttempt attempt)
    {
        string failure = $"{attempt.StandardError}\n{attempt.StandardOutput}";
        return Http429Regex().IsMatch(failure) ||
            failure.Contains("toomanyrequests", StringComparison.OrdinalIgnoreCase) ||
            failure.Contains("too many requests", StringComparison.OrdinalIgnoreCase) ||
            failure.Contains("pull rate limit", StringComparison.OrdinalIgnoreCase) ||
            failure.Contains("rate limit exceeded", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetRegistry(string image)
    {
        string firstComponent = image.Split('/', 2)[0];
        bool hasRegistryPath = image.Contains('/', StringComparison.Ordinal);
        return hasRegistryPath && (
            firstComponent.Contains('.', StringComparison.Ordinal) ||
            firstComponent.Contains(':', StringComparison.Ordinal) ||
            firstComponent.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        ) ? firstComponent
            : "docker.io";
    }

    private async Task<ImagePullResult> PrepareCoreAsync(
        string image,
        CancellationToken cancellationToken)
    {
        string registry = GetRegistry(image);
        if (await client.ExistsAsync(image, cancellationToken))
        {
            var local = new ImagePullResult(image, registry, "local", 0, 0, 0);
            await telemetry.RecordResultAsync(local, cancellationToken);
            return local;
        }

        int rateLimits = 0;
        long cumulativeDelay = 0;
        for (int attempt = 1; attempt <= options.MaximumAttempts; attempt++)
        {
            ImagePullAttempt pull = await client.PullAsync(image, cancellationToken);
            if (pull.Succeeded)
            {
                DecayPressure(registry);
                var succeeded = new ImagePullResult(
                    image,
                    registry,
                    "pulled",
                    attempt,
                    rateLimits,
                    cumulativeDelay);
                await CompleteAsync(succeeded, cancellationToken);
                return succeeded;
            }

            if (!IsRateLimitFailure(pull))
            {
                var failed = new ImagePullResult(
                    image,
                    registry,
                    "failed",
                    attempt,
                    rateLimits,
                    cumulativeDelay,
                    SelectError(pull));
                await CompleteAsync(failed, cancellationToken);
                throw new ImagePullException(failed);
            }

            rateLimits++;
            int pressure = registryPressure.AddOrUpdate(
                registry,
                1,
                static (_, current) => Math.Min(current + 1, 30));
            bool exhausted = attempt == options.MaximumAttempts;
            TimeSpan selectedDelay = exhausted
                ? TimeSpan.Zero
                : SelectDelay(Math.Max(rateLimits, pressure));
            cumulativeDelay += (long)selectedDelay.TotalMilliseconds;
            var rateLimitEvent = new ImageRateLimitEvent(
                ImagePullSchema.Version,
                registry,
                image,
                attempt,
                (long)selectedDelay.TotalMilliseconds,
                exhausted ? "exhausted" : "retrying");
            await telemetry.RecordRateLimitAsync(rateLimitEvent, cancellationToken);
            console.RateLimitRetry(rateLimitEvent);

            if (exhausted)
            {
                var failed = new ImagePullResult(
                    image,
                    registry,
                    "exhausted",
                    attempt,
                    rateLimits,
                    cumulativeDelay,
                    SelectError(pull));
                await CompleteAsync(failed, cancellationToken);
                throw new ImagePullException(failed);
            }

            await delay.DelayAsync(selectedDelay, cancellationToken);
        }

        throw new InvalidOperationException("image pull retry loop ended unexpectedly");
    }

    private async Task CompleteAsync(ImagePullResult result, CancellationToken cancellationToken)
    {
        await telemetry.RecordResultAsync(result, cancellationToken);
        if (result.RateLimitResponses > 0)
        {
            console.RateLimitSummary(result);
        }
    }

    private TimeSpan SelectDelay(int pressure)
    {
        double milliseconds = options.InitialDelay.TotalMilliseconds;
        for (int exponent = 1; exponent < pressure; exponent++)
        {
            milliseconds = Math.Min(milliseconds * 2, options.MaximumDelay.TotalMilliseconds);
        }

        double jitter = 1 + (((random.NextDouble() * 2) - 1) * options.JitterRatio);
        return TimeSpan.FromMilliseconds(Math.Min(
            milliseconds * jitter,
            options.MaximumDelay.TotalMilliseconds));
    }

    private void DecayPressure(string registry)
    {
        registryPressure.AddOrUpdate(
            registry,
            0,
            static (_, current) => Math.Max(0, current - 1));
    }

    private static string NormalizeImage(string image)
    {
        if (string.IsNullOrWhiteSpace(image))
        {
            throw new ArgumentException("image reference must not be empty", nameof(image));
        }

        return image.Trim();
    }

    private static string SelectError(ImagePullAttempt attempt) =>
        string.IsNullOrWhiteSpace(attempt.StandardError)
            ? attempt.StandardOutput.Trim()
            : attempt.StandardError.Trim();

    [GeneratedRegex(@"(?:^|\D)429(?:\D|$)", RegexOptions.CultureInvariant)]
    private static partial Regex Http429Regex();
}
