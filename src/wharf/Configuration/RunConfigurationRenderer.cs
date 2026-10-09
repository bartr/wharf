using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wharf.Configuration;

public static class RunConfigurationRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    public static string Render(RunConfiguration configuration, string? format)
    {
        RunConfigurationView view = new(
            configuration.P,
            configuration.CliVersion,
            configuration.Effort,
            configuration.K,
            configuration.Model,
            configuration.N,
            new OutputSettings(configuration.OutputFormat, configuration.OutputDirectory),
            configuration.ResourcePolicy,
            configuration.RetentionDays,
            configuration.RetainWorkspaces,
            configuration.ShortestFirst,
            configuration.Retries,
            configuration.Timeouts,
            configuration.Mounts,
            configuration.Artifacts,
            new SecretSettings(
                Redact(configuration.Credentials.ApiKey),
                Redact(configuration.Credentials.Token),
                Redact(configuration.Credentials.Password)));

        return (format ?? configuration.OutputFormat).Equals("json", StringComparison.Ordinal)
            ? JsonSerializer.Serialize(view, JsonOptions)
            : RenderText(view);
    }

    private static string RenderText(RunConfigurationView view)
    {
        List<string> lines =
        [
            $"p\t{view.P}",
            $"cliVersion\t{view.CliVersion}",
            $"effort\t{view.Effort}",
            $"k\t{view.K}",
            $"model\t{view.Model}",
            $"n\t{view.N}",
            $"output.directory\t{view.Output.Directory}",
            $"output.format\t{view.Output.Format}",
            $"resourcePolicy\t{view.ResourcePolicy}",
            $"retentionDays\t{view.RetentionDays}",
            $"retainWorkspaces\t{view.RetainWorkspaces.ToString().ToLowerInvariant()}",
            $"shortestFirst\t{view.ShortestFirst.ToString().ToLowerInvariant()}",
            $"retries.backoffSeconds\t{view.Retries.BackoffSeconds}",
            $"retries.maxAttempts\t{view.Retries.MaxAttempts}",
            $"timeouts.agentSeconds\t{view.Timeouts.AgentSeconds}",
            $"timeouts.reportSeconds\t{view.Timeouts.ReportSeconds}",
            $"timeouts.setupSeconds\t{view.Timeouts.SetupSeconds}",
            $"timeouts.verifierSeconds\t{view.Timeouts.VerifierSeconds}"
        ];

        return string.Join(Environment.NewLine, lines);
    }

    private static string Redact(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : "[REDACTED]";

    private sealed record RunConfigurationView(
        [property: JsonPropertyName("p")] int P,
        [property: JsonPropertyName("cliVersion")] string CliVersion,
        [property: JsonPropertyName("effort")] string Effort,
        [property: JsonPropertyName("k")] int K,
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("n")] int N,
        [property: JsonPropertyName("output")] OutputSettings Output,
        [property: JsonPropertyName("resourcePolicy")] string ResourcePolicy,
        [property: JsonPropertyName("retentionDays")] int RetentionDays,
        [property: JsonPropertyName("retainWorkspaces")] bool RetainWorkspaces,
        [property: JsonPropertyName("shortestFirst")] bool ShortestFirst,
        [property: JsonPropertyName("retries")] RetryPolicy Retries,
        [property: JsonPropertyName("timeouts")] PhaseTimeouts Timeouts,
        [property: JsonPropertyName("mounts")] IReadOnlyList<MountSettings> Mounts,
        [property: JsonPropertyName("artifacts")] IReadOnlyList<string> Artifacts,
        [property: JsonPropertyName("credentials")] SecretSettings Credentials);
}
