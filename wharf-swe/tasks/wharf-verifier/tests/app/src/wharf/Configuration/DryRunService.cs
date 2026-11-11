namespace Wharf.Configuration;

public static class DryRunService
{
    public static string Execute(
        string? manifestPath,
        RunConfigurationOverrides overrides,
        string? format,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        RunManifest? manifest = manifestPath is null
            ? null
            : RunManifestLoader.Load(manifestPath);
        RunConfiguration configuration = RunConfigurationResolver.Resolve(
            manifest,
            overrides,
            environment ?? Environment.GetEnvironmentVariables()
                .Cast<System.Collections.DictionaryEntry>()
                .ToDictionary(
                    entry => (string)entry.Key,
                    entry => entry.Value?.ToString(),
                    StringComparer.OrdinalIgnoreCase));
        RunConfigurationValidator.Validate(manifest, configuration);
        return RunConfigurationRenderer.Render(configuration, format ?? configuration.OutputFormat);
    }
}
