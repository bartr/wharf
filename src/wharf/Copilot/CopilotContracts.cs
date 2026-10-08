using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Wharf.Copilot;

public enum CopilotCredentialSourceKind
{
    Environment,
    File,
    Stdin
}

public sealed partial record CopilotCredentialSource(
    CopilotCredentialSourceKind Kind,
    string Reference)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Reference))
        {
            throw new ArgumentException("credential source reference must not be empty", nameof(Reference));
        }

        if (Kind is CopilotCredentialSourceKind.Environment &&
            !EnvironmentVariableRegex().IsMatch(Reference))
        {
            throw new ArgumentException("credential environment reference is not a valid variable name", nameof(Reference));
        }
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex EnvironmentVariableRegex();
}

public sealed record CopilotAdapterOptions(
    string RequestedVersion,
    string Model,
    string NativeEffort,
    string FrontierEffort,
    IReadOnlyList<string> SubagentModels,
    CopilotCredentialSource Credential,
    IReadOnlyList<string> Endpoints,
    IReadOnlySet<string> AllowedEndpointHosts);

public sealed record CopilotCliVersion(string Value);

public sealed record CopilotInvocation(
    string Executable,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment,
    bool ReadsCredentialFromStdin,
    string RequestedVersion,
    string EffectiveVersion,
    string Model,
    string NativeEffort,
    string FrontierEffort,
    IReadOnlyList<string> SubagentModels,
    IReadOnlyList<string> Endpoints,
    string? CredentialValue = null)
{
    public string RedactedCommand =>
        string.Join(
            ' ',
            new[] { Executable }.Concat(Arguments.Select(Quote)));

    private static string Quote(string value) =>
        value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"" : value;
}

public interface ICopilotCliProbe
{
    Task<CopilotCliVersion> GetVersionAsync(CancellationToken cancellationToken);
}

public sealed class CopilotAdapterException(string message) : Exception(message);

public sealed partial class CopilotAdapter
{
    private readonly ICopilotCliProbe probe;
    private readonly string executable;

    public CopilotAdapter(ICopilotCliProbe probe, string executable = "copilot")
    {
        this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
        this.executable = string.IsNullOrWhiteSpace(executable)
            ? throw new ArgumentException("CLI executable must not be empty", nameof(executable))
            : executable;
    }

    public async Task<CopilotInvocation> PrepareAsync(
        CopilotAdapterOptions options,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        CopilotCliVersion effective = await probe.GetVersionAsync(cancellationToken);
        string effectiveVersion = NormalizeVersion(effective.Value, "effective");
        string requestedVersion = options.RequestedVersion.Equals(
                "latest",
                StringComparison.OrdinalIgnoreCase)
            ? effectiveVersion
            : NormalizeVersion(options.RequestedVersion, "requested");
        if (!string.Equals(requestedVersion, effectiveVersion, StringComparison.Ordinal))
        {
            throw new CopilotAdapterException(
                $"requested Copilot CLI {requestedVersion} does not match effective version {effectiveVersion}");
        }

        string normalizedFrontierEffort = NormalizeFrontierEffort(options.FrontierEffort);
        bool isHydraFusion = options.Model.Equals("hydrafusion", StringComparison.OrdinalIgnoreCase);
        var arguments = new List<string>
        {
            "--model", options.Model,
            "--allow-all-tools",
            "--no-auto-update"
        };
        if (!isHydraFusion)
        {
            arguments.Add("--reasoning-effort");
            arguments.Add(options.NativeEffort);
        }

        if (isHydraFusion)
        {
            arguments.Add("--experimental");
            arguments.Add("--log-level");
            arguments.Add("all");
            arguments.Add("--log-dir");
            arguments.Add("/workspace/.copilot-logs");
        }
        foreach (string subagent in options.SubagentModels)
        {
            arguments.Add("--subagent-model");
            arguments.Add(subagent);
        }

        foreach (string endpoint in options.Endpoints)
        {
            arguments.Add("--endpoint");
            arguments.Add(endpoint);
        }

        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["WHARF_COPILOT_CREDENTIAL_SOURCE"] = options.Credential.Kind.ToString().ToLowerInvariant()
        };
        bool stdin = options.Credential.Kind is CopilotCredentialSourceKind.Stdin;
        if (options.Credential.Kind is CopilotCredentialSourceKind.Environment)
        {
            environment["WHARF_COPILOT_CREDENTIAL_VARIABLE"] = options.Credential.Reference;
        }
        else if (options.Credential.Kind is CopilotCredentialSourceKind.File)
        {
            string path = ExpandHome(options.Credential.Reference);
            if (!File.Exists(path))
            {
                throw new CopilotAdapterException($"credential file does not exist: {path}");
            }

            string credential = File.ReadAllText(path).Trim();
            if (string.IsNullOrWhiteSpace(credential))
            {
                throw new CopilotAdapterException($"credential file is empty: {path}");
            }

            environment["WHARF_COPILOT_CREDENTIAL_VARIABLE"] = "GH_TOKEN";
            return new CopilotInvocation(
                executable,
                arguments,
                environment,
                stdin,
                requestedVersion,
                effectiveVersion,
                options.Model,
                options.NativeEffort,
                normalizedFrontierEffort,
                options.SubagentModels,
                options.Endpoints,
                credential);
        }
        else
        {
            arguments.Add("--credential-stdin");
        }

        return new CopilotInvocation(
            executable,
            arguments,
            environment,
            stdin,
            requestedVersion,
            effectiveVersion,
            options.Model,
            options.NativeEffort,
            normalizedFrontierEffort,
            options.SubagentModels,
            options.Endpoints);
    }

    private static void ValidateOptions(CopilotAdapterOptions options)
    {
        if (options is null)
        {
            throw new CopilotAdapterException("Copilot adapter options are required");
        }

        if (string.IsNullOrWhiteSpace(options.RequestedVersion))
        {
            throw new CopilotAdapterException("requested Copilot CLI version is required");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            throw new CopilotAdapterException("Copilot model is required");
        }

        ValidateEffort(options.NativeEffort, "native effort");
        NormalizeFrontierEffort(options.FrontierEffort);
        options.Credential.Validate();
        foreach (string subagent in options.SubagentModels)
        {
            if (string.IsNullOrWhiteSpace(subagent))
            {
                throw new CopilotAdapterException("subagent model names must not be empty");
            }
        }

        foreach (string endpoint in options.Endpoints)
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri) ||
                uri.Scheme is not "https" ||
                !options.AllowedEndpointHosts.Contains(uri.Host))
            {
                throw new CopilotAdapterException($"endpoint is not allowed: {endpoint}");
            }
        }
    }

    private static string NormalizeVersion(string version, string name)
    {
        string normalized = version.Trim();
        normalized = normalized.TrimStart('v');
        if (!SemverRegex().IsMatch(normalized))
        {
            throw new CopilotAdapterException($"{name} Copilot CLI version is invalid: {version}");
        }

        return normalized;
    }

    private static string NormalizeFrontierEffort(string effort) =>
        effort.Trim().ToLowerInvariant() switch
        {
            "low" => "low",
            "medium" => "medium",
            "high" => "high",
            "xhigh" => "high",
            "max" => "max",
            _ => throw new CopilotAdapterException($"unsupported frontier effort: {effort}")
        };

    private static string ExpandHome(string path) =>
        path == "~"
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : path.StartsWith("~/", StringComparison.Ordinal)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    path[2..])
                : path;

    private static void ValidateEffort(string effort, string name)
    {
        if (effort.Trim().ToLowerInvariant() is not ("low" or "medium" or "high" or "max"))
        {
            throw new CopilotAdapterException($"unsupported {name}: {effort}");
        }
    }

    [GeneratedRegex(
        @"^\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:\.[0-9A-Za-z]+)*)?(?:\+[0-9A-Za-z]+(?:\.[0-9A-Za-z]+)*)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SemverRegex();
}

public sealed class ProcessCopilotCliProbe(string executable = "copilot") : ICopilotCliProbe
{
    public async Task<CopilotCliVersion> GetVersionAsync(CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(executable, "--version")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using Process process = Process.Start(startInfo)
            ?? throw new CopilotAdapterException("Copilot CLI could not be started");
        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        string error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            throw new CopilotAdapterException(
                $"Copilot CLI version probe failed: {error.Trim()}");
        }

        Match match = Regex.Match(
            output,
            @"(?<!\d)v?\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:\.[0-9A-Za-z]+)*)?(?:\+[0-9A-Za-z]+(?:\.[0-9A-Za-z]+)*)?",
            RegexOptions.CultureInvariant);
        if (!match.Success)
            throw new CopilotAdapterException("Copilot CLI returned no semantic version");
        return new CopilotCliVersion(match.Value);
    }
}
