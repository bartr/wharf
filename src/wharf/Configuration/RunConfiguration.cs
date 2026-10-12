using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wharf.Configuration;

public sealed record RunManifest(
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion = "1.0",
    [property: JsonPropertyName("k")] int? K = null,
    [property: JsonPropertyName("n")] int? N = null,
    [property: JsonPropertyName("p")] int? P = null,
    [property: JsonPropertyName("timeouts")] PhaseTimeouts? Timeouts = null,
    [property: JsonPropertyName("retries")] RetryPolicy? Retries = null,
    [property: JsonPropertyName("output")] OutputSettings? Output = null,
    [property: JsonPropertyName("model")] string? Model = null,
    [property: JsonPropertyName("effort")] string? Effort = null,
    [property: JsonPropertyName("cliVersion")] string? CliVersion = null,
    [property: JsonPropertyName("mounts")] IReadOnlyList<MountSettings>? Mounts = null,
    [property: JsonPropertyName("artifacts")] IReadOnlyList<string>? Artifacts = null,
    [property: JsonPropertyName("retentionDays")] int? RetentionDays = null,
    [property: JsonPropertyName("retainWorkspaces")] bool? RetainWorkspaces = null,
    [property: JsonPropertyName("resourcePolicy")] string? ResourcePolicy = null,
    [property: JsonPropertyName("credentials")] SecretSettings? Credentials = null);

public sealed record PhaseTimeouts(
    [property: JsonPropertyName("setupSeconds")] int? SetupSeconds = null,
    [property: JsonPropertyName("agentSeconds")] int? AgentSeconds = null,
    [property: JsonPropertyName("verifierSeconds")] int? VerifierSeconds = null,
    [property: JsonPropertyName("reportSeconds")] int? ReportSeconds = null);

public sealed record RetryPolicy(
    [property: JsonPropertyName("maxAttempts")] int? MaxAttempts = null,
    [property: JsonPropertyName("backoffSeconds")] int? BackoffSeconds = null);

public sealed record OutputSettings(
    [property: JsonPropertyName("format")] string? Format = null,
    [property: JsonPropertyName("directory")] string? Directory = null);

public sealed record MountSettings(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("readOnly")] bool ReadOnly = true);

public sealed record SecretSettings(
    [property: JsonPropertyName("apiKey")] string? ApiKey = null,
    [property: JsonPropertyName("token")] string? Token = null,
    [property: JsonPropertyName("password")] string? Password = null);

public sealed record RunConfiguration(
    int K,
    int N,
    int P,
    PhaseTimeouts Timeouts,
    RetryPolicy Retries,
    string OutputFormat,
    string OutputDirectory,
    string Model,
    string Effort,
    string CliVersion,
    IReadOnlyList<MountSettings> Mounts,
    IReadOnlyList<string> Artifacts,
    int RetentionDays,
    bool RetainWorkspaces,
    string ResourcePolicy,
    SecretSettings Credentials,
    bool ShortestFirst = false);

public sealed record RunConfigurationOverrides(
    int? K = null,
    int? N = null,
    int? P = null,
    string? OutputFormat = null,
    string? OutputDirectory = null,
    string? Model = null,
    string? Effort = null,
    bool? RetainWorkspaces = null,
    bool? ShortestFirst = null);

public sealed class RunConfigurationException(string message) : Exception(message);

public static class RunManifestLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static RunManifest Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new RunConfigurationException("manifest path must not be empty");
        }

        if (!File.Exists(path))
        {
            throw new RunConfigurationException($"manifest does not exist: {path}");
        }

        try
        {
            string json = File.ReadAllText(path);
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                throw new RunConfigurationException("manifest must contain a JSON object");
            }

            if (!document.RootElement.TryGetProperty("schemaVersion", out _))
            {
                throw new RunConfigurationException("manifest must specify schemaVersion");
            }

            return JsonSerializer.Deserialize<RunManifest>(json, Options)
                ?? throw new RunConfigurationException("manifest must contain a JSON object");
        }
        catch (JsonException exception)
        {
            throw new RunConfigurationException($"invalid manifest: {exception.Message}");
        }
        catch (IOException exception)
        {
            throw new RunConfigurationException($"cannot read manifest: {exception.Message}");
        }
    }
}

public static class RunConfigurationResolver
{
    public const string DefaultCopilotCli = "copilot";
    public const string DefaultCopilotModel = "gpt-5.6-luna";
    public const string DefaultCopilotVersion = "latest";

    public static string ResolveEffectiveEffort(string model, string effort) =>
        model.Equals("hydrafusion", StringComparison.OrdinalIgnoreCase) &&
        RunConfigurationValidator.IsSupportedEffort(effort)
            ? "default"
            : effort;

    public static RunConfiguration Resolve(
        RunManifest? manifest,
        RunConfigurationOverrides overrides,
        IReadOnlyDictionary<string, string?> environment)
    {
        manifest ??= new RunManifest();
        string? EnvironmentValue(string name)
        {
            if (name.StartsWith("WHARF_", StringComparison.Ordinal) &&
                environment.TryGetValue(name["WHARF_".Length..], out string? shortValue) &&
                !string.IsNullOrWhiteSpace(shortValue))
                return shortValue;

            return environment.TryGetValue(name, out string? value) ? value : null;
        }

        int k = PickInt(overrides.K, EnvironmentValue("WHARF_K"), manifest.K, 1, "K");
        int n = PickInt(overrides.N, EnvironmentValue("WHARF_N"), manifest.N, 4, "N");
        int p = PickInt(overrides.P, EnvironmentValue("WHARF_P"), manifest.P, 4, "P");
        p = Math.Min(p, n);
        string format = Pick(
            overrides.OutputFormat,
            EnvironmentValue("WHARF_OUTPUT_FORMAT"),
            manifest.Output?.Format,
            "text");
        string directory = Pick(
            overrides.OutputDirectory,
            EnvironmentValue("WHARF_OUTPUT_DIRECTORY"),
            manifest.Output?.Directory,
            "jobs");
        string model = Pick(overrides.Model, EnvironmentValue("WHARF_MODEL"), manifest.Model, DefaultCopilotModel);
        string effort = Pick(overrides.Effort, EnvironmentValue("WHARF_EFFORT"), manifest.Effort, "max");
        effort = ResolveEffectiveEffort(model, effort);
        SecretSettings credentials = new(
            EnvironmentValue("WHARF_API_KEY") ?? manifest.Credentials?.ApiKey,
            EnvironmentValue("WHARF_TOKEN") ?? manifest.Credentials?.Token,
            EnvironmentValue("WHARF_PASSWORD") ?? manifest.Credentials?.Password);
        RetryPolicy retries = manifest.Retries ?? new RetryPolicy();
        retries = retries with { MaxAttempts = retries.MaxAttempts ?? 1 };
        bool retainWorkspaces = PickBool(
            overrides.RetainWorkspaces,
            EnvironmentValue("WHARF_RETAIN_WORKSPACES"),
            manifest.RetainWorkspaces,
            false,
            "retainWorkspaces");

        return new RunConfiguration(
            k,
            n,
            p,
            manifest.Timeouts ?? new PhaseTimeouts(),
            retries,
            format,
            directory,
            model,
            effort,
            manifest.CliVersion ?? DefaultCopilotVersion,
            manifest.Mounts ?? [],
            manifest.Artifacts ?? [],
            manifest.RetentionDays ?? 30,
            retainWorkspaces,
            manifest.ResourcePolicy ?? "default",
            credentials,
            overrides.ShortestFirst ?? false);
    }

    private static int PickInt(int? flag, string? environment, int? manifest, int fallback, string name)
    {
        string? value = flag?.ToString() ?? environment ?? manifest?.ToString() ?? fallback.ToString();
        if (!int.TryParse(value, out int result))
        {
            throw new RunConfigurationException($"{name} must be an integer");
        }

        return result;
    }

    private static string Pick(string? flag, string? environment, string? manifest, string fallback) =>
        flag ?? environment ?? manifest ?? fallback;

    private static bool PickBool(
        bool? flag,
        string? environment,
        bool? manifest,
        bool fallback,
        string name)
    {
        if (flag.HasValue)
            return flag.Value;
        if (string.IsNullOrWhiteSpace(environment))
            return manifest ?? fallback;
        if (bool.TryParse(environment, out bool result))
            return result;
        if (environment == "1")
            return true;
        if (environment == "0")
            return false;
        throw new RunConfigurationException($"{name} must be true, false, 1, or 0");
    }
}

public static class RunConfigurationValidator
{
    private static readonly HashSet<string> Formats = ["text", "json"];
    private static readonly HashSet<string> Efforts = ["default", "low", "medium", "high", "max"];
    private static readonly HashSet<string> Policies = ["default", "restricted", "unrestricted"];

    internal static bool IsSupportedEffort(string effort) => Efforts.Contains(effort);

    public static void Validate(RunManifest? manifest, RunConfiguration configuration)
    {
        if (manifest is not null && manifest.SchemaVersion is not "1.0")
        {
            throw new RunConfigurationException(
                $"unsupported manifest schema version: {manifest.SchemaVersion}");
        }

        RequirePositive(configuration.K, "K");
        RequirePositive(configuration.N, "N");
        RequirePositive(configuration.P, "P");
        ValidateTimeout(configuration.Timeouts.SetupSeconds, "setupSeconds");
        ValidateTimeout(configuration.Timeouts.AgentSeconds, "agentSeconds");
        ValidateTimeout(configuration.Timeouts.VerifierSeconds, "verifierSeconds");
        ValidateTimeout(configuration.Timeouts.ReportSeconds, "reportSeconds");
        ValidateTimeout(configuration.Retries.MaxAttempts, "maxAttempts", allowZero: true);
        ValidateTimeout(configuration.Retries.BackoffSeconds, "backoffSeconds", allowZero: true);
        ValidateValue(configuration.OutputFormat, Formats, "output format");
        ValidateValue(configuration.Effort, Efforts, "effort");
        ValidateValue(configuration.ResourcePolicy, Policies, "resource policy");

        if (configuration.RetentionDays < 0)
        {
            throw new RunConfigurationException("retentionDays must not be negative");
        }

        if (string.IsNullOrWhiteSpace(configuration.OutputDirectory) ||
            Path.IsPathRooted(configuration.OutputDirectory) ||
            configuration.OutputDirectory
                .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
                .Any(part => part == ".."))
        {
            throw new RunConfigurationException(
                "output directory must be a non-empty relative path");
        }

        foreach (MountSettings mount in configuration.Mounts)
        {
            if (string.IsNullOrWhiteSpace(mount.Source) || string.IsNullOrWhiteSpace(mount.Target))
            {
                throw new RunConfigurationException("mount source and target must not be empty");
            }
        }
    }

    private static void RequirePositive(int value, string name)
    {
        if (value <= 0)
        {
            throw new RunConfigurationException($"{name} must be positive");
        }
    }

    private static void ValidateTimeout(int? value, string name, bool allowZero = false)
    {
        if (value is not null && (allowZero ? value < 0 : value <= 0))
        {
            throw new RunConfigurationException($"{name} must be {(allowZero ? "non-negative" : "positive")}");
        }
    }

    private static void ValidateValue(string value, HashSet<string> allowed, string name)
    {
        if (!allowed.Contains(value))
        {
            throw new RunConfigurationException(
                $"unsupported {name}: {value}; allowed values: {string.Join(", ", allowed.Order())}");
        }
    }
}
