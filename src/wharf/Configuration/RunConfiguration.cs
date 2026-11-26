using System.Text.Json;
using System.Text.Json.Serialization;
using Wharf.Agent;

namespace Wharf.Configuration;

public sealed record UserConfiguration(
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion = "1.0",
    [property: JsonPropertyName("k")] int? K = null,
    [property: JsonPropertyName("n")] int? N = null,
    [property: JsonPropertyName("p")] int? P = null,
    [property: JsonPropertyName("timeouts")] PhaseTimeouts? Timeouts = null,
    [property: JsonPropertyName("retries")] RetryPolicy? Retries = null,
    [property: JsonPropertyName("outputFormat")] string? OutputFormat = null,
    [property: JsonPropertyName("retainWorkspaces")] bool? RetainWorkspaces = null,
    [property: JsonPropertyName("containerCpus")] int? ContainerCpus = null,
    [property: JsonPropertyName("shortestFirst")] bool? ShortestFirst = null);

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
    bool ShortestFirst = false,
    int? ContainerCpus = null,
    bool NoOp = false,
    bool Oracle = false,
    string Cli = "copilot");

public sealed record RunConfigurationOverrides(
    int? K = null,
    int? N = null,
    int? P = null,
    string? OutputFormat = null,
    string? OutputDirectory = null,
    string? Model = null,
    string? Effort = null,
    bool? RetainWorkspaces = null,
    bool? ShortestFirst = null,
    string? CliVersion = null,
    int? ContainerCpus = null,
    bool? NoOp = null,
    bool? Oracle = null,
    string? Cli = null);

public sealed class RunConfigurationException(string message) : Exception(message);

public static class UserConfigurationLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static UserConfiguration Load(string? homeDirectory = null)
    {
        string path = ConfigurationPath(homeDirectory);
        try
        {
            string json = File.ReadAllText(path);
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                throw new RunConfigurationException($"user configuration must contain a JSON object: {path}");
            }

            ValidateUniqueProperties(document.RootElement);
            UserConfiguration defaults = JsonSerializer.Deserialize<UserConfiguration>(json, Options)
                ?? throw new RunConfigurationException($"user configuration must contain a JSON object: {path}");
            if (defaults.SchemaVersion is not "1.0")
                throw new RunConfigurationException($"unsupported user configuration schema version: {defaults.SchemaVersion}");
            RunConfigurationValidator.Validate(RunConfigurationResolver.Resolve(
                defaults, new RunConfigurationOverrides(), new Dictionary<string, string?>()));
            return defaults;
        }
        catch (FileNotFoundException)
        {
            return new UserConfiguration();
        }
        catch (DirectoryNotFoundException)
        {
            return new UserConfiguration();
        }
        catch (JsonException exception)
        {
            throw new RunConfigurationException($"invalid user configuration at {path}: {exception.Message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new RunConfigurationException($"cannot read user configuration at {path}: {exception.Message}");
        }
    }

    public static void EnsureExists(string? homeDirectory = null)
    {
        string path = ConfigurationPath(homeDirectory);
        string temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            string directory = Path.GetDirectoryName(path)!;
            if (OperatingSystem.IsLinux())
                Directory.CreateDirectory(directory,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            else
                Directory.CreateDirectory(directory);
            if (File.Exists(path))
                return;

            RunConfiguration configuration = RunConfigurationResolver.Resolve(
                null, new RunConfigurationOverrides(), new Dictionary<string, string?>());
            UserConfiguration defaults = new(
                K: configuration.K,
                N: configuration.N,
                P: configuration.P,
                Timeouts: configuration.Timeouts,
                Retries: configuration.Retries,
                OutputFormat: configuration.OutputFormat,
                RetainWorkspaces: configuration.RetainWorkspaces,
                ContainerCpus: configuration.ContainerCpus,
                ShortestFirst: configuration.ShortestFirst);
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(defaults, new JsonSerializerOptions(Options)
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                }) + Environment.NewLine);
                try
                {
                    File.Move(temporary, path);
                }
                catch (IOException) when (File.Exists(path))
                {
                    // Another invocation created the configuration first.
                }
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new RunConfigurationException($"cannot initialize user configuration at {path}: {exception.Message}");
        }
    }

    private static string ConfigurationPath(string? homeDirectory)
    {
        string home = homeDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
            throw new RunConfigurationException("cannot locate the user home directory for Wharf configuration");
        return Path.Combine(home, ".wharf", "config.json");
    }

    private static void ValidateUniqueProperties(JsonElement element)
    {
        if (element.ValueKind is not JsonValueKind.Object)
            return;
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!names.Add(property.Name))
                throw new RunConfigurationException($"duplicate user configuration property: {property.Name}");
            ValidateUniqueProperties(property.Value);
        }
    }
}

public static class RunConfigurationResolver
{
    public const string DefaultCopilotCli = "copilot";
    public const string DefaultCopilotModel = "hydrafusion";
    public const string DefaultClaudeModel = "claude-sonnet-5-5";
    public const string DefaultCodexModel = "gpt-6-luna";
    public const string DefaultCopilotVersion = "latest";

    internal static IReadOnlyDictionary<string, string?> ReadEnvironment() =>
        new[]
        {
            "K", "N", "P", "MODEL", "WHARF_K", "WHARF_N", "WHARF_P",
            "WHARF_CONTAINER_CPUS", "WHARF_OUTPUT_FORMAT", "WHARF_OUTPUT_DIRECTORY",
            "WHARF_MODEL", "WHARF_EFFORT", "WHARF_CLI", "WHARF_RETAIN_WORKSPACES",
            "WHARF_CLI_VERSION", "CLI_VERSION", "WHARF_API_KEY", "WHARF_TOKEN", "WHARF_PASSWORD"
        }.ToDictionary(name => name, Environment.GetEnvironmentVariable, StringComparer.Ordinal);

    public static string ResolveEffectiveEffort(string model, string effort) =>
        model.Equals("hydrafusion", StringComparison.OrdinalIgnoreCase) &&
        RunConfigurationValidator.IsSupportedEffort(effort)
            ? "default"
            : effort;

    public static RunConfiguration Resolve(
        UserConfiguration? defaults,
        RunConfigurationOverrides overrides,
        IReadOnlyDictionary<string, string?> environment)
    {
        RejectObsoleteVersionEnvironment(environment);
        defaults ??= new UserConfiguration();
        string? EnvironmentValue(string name)
        {
            if (name.StartsWith("WHARF_", StringComparison.Ordinal) &&
                environment.TryGetValue(name["WHARF_".Length..], out string? shortValue) &&
                !string.IsNullOrWhiteSpace(shortValue))
                return shortValue;

            return environment.TryGetValue(name, out string? value) ? value : null;
        }

        int k = PickInt(overrides.K, EnvironmentValue("WHARF_K"), defaults.K, 1, "K");
        int n = PickInt(overrides.N, EnvironmentValue("WHARF_N"), defaults.N, 4, "N");
        int p = PickInt(overrides.P, EnvironmentValue("WHARF_P"), defaults.P, 4, "P");
        if (p <= 0)
            throw new RunConfigurationException("P must be positive");
        int? containerCpus = PickOptionalInt(
            overrides.ContainerCpus,
            environment.GetValueOrDefault("WHARF_CONTAINER_CPUS"),
            defaults.ContainerCpus,
            "containerCpus");
        p = Math.Min(p, n);
        string format = Pick(
            overrides.OutputFormat,
            EnvironmentValue("WHARF_OUTPUT_FORMAT"),
            defaults.OutputFormat,
            "text");
        string directory = Pick(
            overrides.OutputDirectory,
            EnvironmentValue("WHARF_OUTPUT_DIRECTORY"),
            null,
            "jobs");
        string cli = Pick(overrides.Cli, environment.GetValueOrDefault("WHARF_CLI"), null, DefaultCopilotCli);
        _ = AgentCli.For(cli);
        string model = Pick(
            overrides.Model, EnvironmentValue("WHARF_MODEL"), null,
            cli switch
            {
                "claude" => DefaultClaudeModel,
                "codex" => DefaultCodexModel,
                _ => DefaultCopilotModel
            });
        string effort = Pick(overrides.Effort, EnvironmentValue("WHARF_EFFORT"), null, "default");
        if (cli == "copilot")
            effort = ResolveEffectiveEffort(model, effort);
        string cliVersion = overrides.CliVersion ?? DefaultCopilotVersion;
        SecretSettings credentials = new(
            EnvironmentValue("WHARF_API_KEY"),
            EnvironmentValue("WHARF_TOKEN"),
            EnvironmentValue("WHARF_PASSWORD"));
        RetryPolicy retries = defaults.Retries ?? new RetryPolicy();
        retries = retries with { MaxAttempts = retries.MaxAttempts ?? 1 };
        bool retainWorkspaces = PickBool(
            overrides.RetainWorkspaces,
            EnvironmentValue("WHARF_RETAIN_WORKSPACES"),
            defaults.RetainWorkspaces,
            false,
            "retainWorkspaces");

        return new RunConfiguration(
            k,
            n,
            p,
            defaults.Timeouts ?? new PhaseTimeouts(),
            retries,
            format,
            directory,
            model,
            effort,
            cliVersion,
            [],
            [],
            30,
            retainWorkspaces,
            "default",
            credentials,
            overrides.ShortestFirst ?? defaults.ShortestFirst ?? false,
            containerCpus,
            overrides.NoOp ?? false,
            overrides.Oracle ?? false,
            cli);
    }

    internal static void RejectObsoleteVersionEnvironment(IReadOnlyDictionary<string, string?> environment)
    {
        foreach (string name in new[] { "WHARF_CLI_VERSION", "CLI_VERSION" })
        {
            if (environment.GetValueOrDefault(name) is not null)
                throw new RunConfigurationException(
                    $"{name} is no longer supported; unset WHARF_CLI_VERSION CLI_VERSION " +
                    "and use --cli-version to pin the selected CLI");
        }
    }

    private static int PickInt(int? flag, string? environment, int? defaults, int fallback, string name) =>
        PickOptionalInt(flag, environment, defaults, name) ?? fallback;

    private static int? PickOptionalInt(int? flag, string? environment, int? defaults, string name)
    {
        string? value = flag?.ToString() ?? environment ?? defaults?.ToString();
        if (value is null)
            return null;
        if (!int.TryParse(value, out int result))
        {
            throw new RunConfigurationException($"{name} must be an integer");
        }

        return result;
    }

    private static string Pick(string? flag, string? environment, string? defaults, string fallback) =>
        flag ?? environment ?? defaults ?? fallback;

    private static bool PickBool(
        bool? flag,
        string? environment,
        bool? defaults,
        bool fallback,
        string name)
    {
        if (flag.HasValue)
            return flag.Value;
        if (string.IsNullOrWhiteSpace(environment))
            return defaults ?? fallback;
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

    internal static void ValidateEffort(string effort) => ValidateValue(effort, Efforts, "effort");

    public static void Validate(RunConfiguration configuration)
    {
        RequirePositive(configuration.K, "K");
        RequirePositive(configuration.N, "N");
        RequirePositive(configuration.P, "P");
        _ = AgentCli.For(configuration.Cli);
        if (configuration.ContainerCpus is { } containerCpus)
            RequirePositive(containerCpus, "containerCpus");
        ValidateTimeout(configuration.Timeouts.SetupSeconds, "setupSeconds");
        ValidateTimeout(configuration.Timeouts.AgentSeconds, "agentSeconds");
        ValidateTimeout(configuration.Timeouts.VerifierSeconds, "verifierSeconds");
        if (configuration.Timeouts.ReportSeconds is not null)
        {
            throw new RunConfigurationException(
                "timeout reportSeconds is not supported by the current run pipeline");
        }
        ValidateTimeout(configuration.Retries.MaxAttempts, "maxAttempts", allowZero: true);
        ValidateTimeout(configuration.Retries.BackoffSeconds, "backoffSeconds", allowZero: true);
        ValidateValue(configuration.OutputFormat, Formats, "output format");
        ValidateEffort(configuration.Effort);
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
