The Wharf 0.5.0 release can prepare Docker images but does not have bounded
image-pull coordination. Add the image pull control boundary from Wharf 0.6.0.

Implement image pull coordination so that it:

- reuses images already present locally without registry access;
- retries only explicit registry rate-limit responses with bounded jittered
  backoff;
- tracks registry pressure and decays it after successful pulls;
- coalesces duplicate image pulls;
- records per-attempt and aggregate pull telemetry;
- exposes verbosity-controlled retry and summary output.

Preserve the historical public contract in the `Wharf.Image` namespace.
Expose `ImagePullCoordinator`, `ImagePullOptions`, `IImagePullTelemetry`,
`IImageClient`, `ImagePullSchema`, `ImagePullAttempt`, `ImagePullResult`,
`ImageRateLimitEvent`, and `ImagePullSummary`. `ImagePullSchema.Version` must
be `"1.0"`, and invalid retry options must be rejected explicitly.

Use these historical public shapes:

- `ImagePullOptions(int MaxRetries, TimeSpan InitialBackoff,
  TimeSpan MaximumBackoff, double JitterRatio)` exposes `Validate()`;
- `ImagePullAttempt(bool Succeeded, string StandardOutput,
  string StandardError)`;
- `IImageClient.ExistsAsync(string, CancellationToken)` and
  `PullAsync(string, CancellationToken)`;
- `IAsyncDelay.DelayAsync(TimeSpan, CancellationToken)`;
- `IRandomSource.NextDouble()`;
- `IImagePullTelemetry.RecordRateLimitAsync(ImageRateLimitEvent,
  CancellationToken)` and `RecordResultAsync(ImagePullResult,
  CancellationToken)`;
- `IImagePullConsole.RateLimitRetry(ImageRateLimitEvent)` and
  `RateLimitSummary(ImagePullResult)`;
- `ImagePullCoordinator(IImageClient, IAsyncDelay, IRandomSource,
  IImagePullTelemetry, IImagePullConsole, ImagePullOptions? options = null)`;
  and
- `PrepareAsync(string image, CancellationToken cancellationToken = default)`
  returns an `ImagePullResult` exposing `Outcome` and `PullAttempts`.

Expose public static `ImagePullCoordinator.IsRateLimitFailure(ImagePullAttempt)`
and `GetRegistry(string)`. Treat explicit HTTP 429 and
`toomanyrequests` responses as rate limits, but not authentication failures.
An image present locally returns outcome `"local"` without calling pull and
records the result through telemetry.

`ImagePullOptions.Validate()` must reject a retry count less than one, a
non-positive initial backoff, a maximum backoff smaller than the initial
backoff, and a jitter ratio outside the inclusive range zero through one.

Keep the agent workspace separate from the pristine verifier workspace and do
not modify the existing test files.

Do not commit changes. The evaluator captures the working-tree patch after
implementation.
