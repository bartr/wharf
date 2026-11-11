The Wharf 0.6.0 release has image pulling but no derived-image coordinator.
Add the derived image build pipeline from Wharf 0.7.0.

Implement derived-image coordination so that it:

- creates deterministic SHA-256 cache keys from base image, ordered scripts,
  sorted versions, and sorted configuration;
- gates base preparation, context preparation, and derived builds together;
- coalesces identical in-flight builds;
- reuses successful builds unless `ForceBuild` is requested;
- lets caller cancellation stop only that caller's wait;
- removes failed in-flight operations so later attempts can retry.

Preserve the historical public contract from 0.7.0. The implementation must
expose `Wharf.Image.DerivedImageCoordinator`,
`Wharf.Image.IDerivedImageBackend`, `Wharf.Image.DerivedImageRequest`,
`Wharf.Image.DerivedImageResult`, and
`Wharf.Image.DerivedImageCacheKey`. `DerivedImageCacheKey` must expose a public
static `Create` method accepting a `DerivedImageRequest`; the request must
expose a public `Validate` method that rejects invalid requests.

Use these historical public shapes:

- `DerivedImageRequest(string BaseImage, IReadOnlyList<string> Scripts,
  IReadOnlyDictionary<string, string> Versions,
  IReadOnlyDictionary<string, string> Configuration,
  bool ForceBuild = false)`;
- `DerivedImageResult(string CacheKey, string Image, bool Reused, bool Forced)`;
- `IDerivedImageBackend.EnsureBaseImageAsync`,
  `PrepareContextAsync(DerivedImageRequest, string cacheKey,
  CancellationToken)`, and
  `BuildAsync(DerivedImageRequest, string cacheKey, CancellationToken)`;
- `DerivedImageCoordinator(IDerivedImageBackend backend, int concurrency)`;
  and
- `DerivedImageCoordinator.PrepareAsync(DerivedImageRequest,
  CancellationToken cancellationToken = default)`.

The coordinator implements `IAsyncDisposable`. A successful non-forced result
is cached; a later identical request returns the same cache key and image with
`Reused` set to true. Script order affects the cache key, while dictionary
insertion order does not.

Keep the agent workspace separate from the pristine verifier workspace and do
not modify the existing test files.

Do not commit changes. The evaluator captures the working-tree patch after
implementation.
