using System.Security.Cryptography;
using System.Text;

namespace Wharf.Image;

public sealed record DerivedImageRequest(
    string BaseImage,
    IReadOnlyList<string> Scripts,
    IReadOnlyDictionary<string, string> Versions,
    IReadOnlyDictionary<string, string> Configuration,
    bool ForceBuild = false)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseImage))
        {
            throw new ArgumentException("base image must not be empty", nameof(BaseImage));
        }

        if (Scripts.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("build scripts must not contain empty values", nameof(Scripts));
        }
    }
}

public sealed record DerivedImageResult(
    string CacheKey,
    string Image,
    bool Reused,
    bool Forced);

public interface IDerivedImageBackend
{
    Task EnsureBaseImageAsync(string image, CancellationToken cancellationToken);
    Task PrepareContextAsync(
        DerivedImageRequest request,
        string cacheKey,
        CancellationToken cancellationToken);
    Task<string> BuildAsync(
        DerivedImageRequest request,
        string cacheKey,
        CancellationToken cancellationToken);
}

public static class DerivedImageCacheKey
{
    public static string Create(DerivedImageRequest request)
    {
        request.Validate();
        var canonical = new StringBuilder();
        Append(canonical, "base", request.BaseImage);
        foreach (string script in request.Scripts)
        {
            Append(canonical, "script", script);
        }

        foreach ((string key, string value) in request.Versions.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            Append(canonical, $"version:{key}", value);
        }

        foreach ((string key, string value) in request.Configuration.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            Append(canonical, $"config:{key}", value);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static void Append(StringBuilder builder, string name, string value)
    {
        builder.Append(name.Length).Append(':').Append(name);
        builder.Append(value.Length).Append(':').Append(value);
    }
}
