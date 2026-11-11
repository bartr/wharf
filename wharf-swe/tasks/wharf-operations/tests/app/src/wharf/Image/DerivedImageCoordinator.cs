using System.Collections.Concurrent;

namespace Wharf.Image;

public sealed class DerivedImageCoordinator : IAsyncDisposable
{
    private readonly IDerivedImageBackend backend;
    private readonly SemaphoreSlim gate;
    private readonly ConcurrentDictionary<string, Lazy<Task<DerivedImageResult>>> inFlight =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DerivedImageResult> cache =
        new(StringComparer.Ordinal);
    private bool disposed;

    public DerivedImageCoordinator(IDerivedImageBackend backend, int concurrency)
    {
        this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
        if (concurrency < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(concurrency), "build concurrency must be positive");
        }

        gate = new SemaphoreSlim(concurrency, concurrency);
    }

    public Task<DerivedImageResult> PrepareAsync(
        DerivedImageRequest request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        request.Validate();
        string cacheKey = DerivedImageCacheKey.Create(request);
        if (!request.ForceBuild && cache.TryGetValue(cacheKey, out DerivedImageResult? cached))
        {
            return Task.FromResult(cached with { Reused = true });
        }

        string inFlightKey = request.ForceBuild
            ? $"{cacheKey}:{Guid.NewGuid():N}"
            : cacheKey;
        Lazy<Task<DerivedImageResult>> pending = inFlight.GetOrAdd(
            inFlightKey,
            key => new Lazy<Task<DerivedImageResult>>(
                () => BuildAndCleanupAsync(key, request, cacheKey),
                LazyThreadSafetyMode.ExecutionAndPublication));
        return WaitForResultAsync(pending.Value, cancellationToken);
    }

    private async Task<DerivedImageResult> WaitForResultAsync(
        Task<DerivedImageResult> operation,
        CancellationToken cancellationToken) =>
        await operation.WaitAsync(cancellationToken);

    private async Task<DerivedImageResult> BuildAndCleanupAsync(
        string inFlightKey,
        DerivedImageRequest request,
        string cacheKey)
    {
        await gate.WaitAsync();
        try
        {
            await backend.EnsureBaseImageAsync(request.BaseImage, CancellationToken.None);
            await backend.PrepareContextAsync(request, cacheKey, CancellationToken.None);
            string image = await backend.BuildAsync(request, cacheKey, CancellationToken.None);
            var result = new DerivedImageResult(cacheKey, image, false, request.ForceBuild);
            if (!request.ForceBuild)
            {
                cache[cacheKey] = result;
            }

            return result;
        }
        finally
        {
            gate.Release();
            inFlight.TryRemove(inFlightKey, out _);
        }
    }

    public ValueTask DisposeAsync()
    {
        if (!disposed)
        {
            disposed = true;
            gate.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
