namespace Wharf.Configuration;

public static class DryRunService
{
    public static string Execute(
        RunConfigurationOverrides overrides,
        string? format,
        IReadOnlyDictionary<string, string?>? environment = null,
        UserConfiguration? defaults = null)
    {
        RunConfiguration configuration = RunConfigurationResolver.Resolve(
            defaults ?? UserConfigurationLoader.Load(),
            overrides,
            environment ?? RunConfigurationResolver.ReadEnvironment());
        RunConfigurationValidator.Validate(configuration);
        return RunConfigurationRenderer.Render(configuration, format ?? configuration.OutputFormat);
    }
}
