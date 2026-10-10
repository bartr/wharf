using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Wharf.Reporting;

public sealed class ReportRenderer
{
    public string RenderMarkdown(ReportInput input)
    {
        Validate(input);
        Wharf.Audit.AuditReport audit = input.Audit;
        StringBuilder report = new();
        report.AppendLine($"# Wharf report: {input.RunId}");
        report.AppendLine();
        report.AppendLine($"- Schema: `{input.SchemaVersion}`");
        report.AppendLine($"- PASS@1: `{Percent(audit.PassAt1)}`");
        report.AppendLine($"- Passing logical attempts: `{audit.PassingAttempts}/{audit.ObservedAttempts}`");
        report.AppendLine($"- Task pass-at-least-once: `{Percent(audit.TaskPassAtLeastOnce)}`");
        report.AppendLine($"- Enterprise Score: `{audit.EnterpriseScore.ToString("0.##", CultureInfo.InvariantCulture)}`");
        report.AppendLine($"- Enterprise coverage component: `{audit.EnterpriseCoverageComponent.ToString("0.##", CultureInfo.InvariantCulture)}`");
        report.AppendLine($"- Enterprise pass component: `{audit.EnterprisePassComponent.ToString("0.##", CultureInfo.InvariantCulture)}`");
        report.AppendLine($"- Completeness: `{(audit.Completeness.IsComplete ? "complete" : "incomplete")}`");
        report.AppendLine();
        report.AppendLine("## Coverage and regressions");
        report.AppendLine();
        report.AppendLine($"- Expected tasks: `{audit.ExpectedTasks}`");
        report.AppendLine($"- Observed tasks: `{audit.ObservedTasks}`");
        report.AppendLine($"- Tasks passing at least once: `{audit.PassingTasks}`");
        report.AppendLine($"- New-test coverage: `{Percent(audit.NewTestCoverage)}`");
        report.AppendLine($"- Existing-test regression: `{audit.ExistingTestRegression}`");
        report.AppendLine();
        report.AppendLine("## Usage and timing");
        report.AppendLine();
        report.AppendLine($"- Cost: `{Number(audit.Cost)}`");
        report.AppendLine($"- Input tokens: `{Number(audit.InputTokens)}`");
        report.AppendLine($"- Output tokens: `{Number(audit.OutputTokens)}`");
        report.AppendLine($"- Cache tokens: `{Number(audit.CacheTokens)}`");
        report.AppendLine($"- Requests: `{Number(audit.Requests)}`");
        report.AppendLine($"- Steps: `{Number(audit.Steps)}`");
        report.AppendLine($"- Agent time: `{Duration(audit.AgentTime)}`");
        report.AppendLine($"- Trial time: `{Duration(audit.TrialTime)}`");
        report.AppendLine();
        report.AppendLine("## Retries and rate limits");
        report.AppendLine();
        report.AppendLine($"- Infrastructure retries: `{input.Retries.InfrastructureRetries}`");
        report.AppendLine($"- Retryable failures: `{input.Retries.RetryableFailures}`");
        report.AppendLine($"- Exhausted retries: `{input.Retries.ExhaustedRetries}`");
        report.AppendLine($"- HTTP 429 responses: `{input.RateLimits.Responses429}`");
        report.AppendLine($"- Affected images: `{input.RateLimits.AffectedImages}`");
        report.AppendLine($"- Exhausted images: `{input.RateLimits.ExhaustedImages}`");
        report.AppendLine($"- Cumulative retry delay: `{input.RateLimits.CumulativeDelayMilliseconds} ms`");
        report.AppendLine();
        report.AppendLine("## Completeness");
        report.AppendLine();
        report.AppendLine($"- Missing tasks: `{audit.Completeness.MissingTasks}`");
        report.AppendLine($"- Missing measurements: `{audit.Completeness.MissingMeasurements}`");
        report.AppendLine($"- Invalid measurements: `{audit.Completeness.InvalidMeasurements}`");
        report.AppendLine($"- Partial measurements: `{audit.Completeness.PartialMeasurements}`");
        report.AppendLine($"- Zero measurements: `{audit.Completeness.ZeroMeasurements}`");
        return report.ToString();
    }

    public string RenderJson(ReportInput input)
    {
        Validate(input);
        return JsonSerializer.Serialize(input, Options);
    }

    public string RenderJsonLines(ReportInput input)
    {
        Validate(input);
        return JsonSerializer.Serialize(input, CompactOptions) + Environment.NewLine;
    }

    public static ReportInput Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new ReportInputException($"report input does not exist: {path}");

        try
        {
            ReportInput? input = JsonSerializer.Deserialize<ReportInput>(
                File.ReadAllText(path), Options);
            return input ?? throw new ReportInputException("report input is empty");
        }
        catch (JsonException exception)
        {
            throw new ReportInputException($"invalid report input: {exception.Message}");
        }
    }

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions CompactOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static void Validate(ReportInput input)
    {
        if (input.SchemaVersion != ReportSchema.Version ||
            string.IsNullOrWhiteSpace(input.RunId) ||
            input.Audit.RunId != input.RunId)
        {
            throw new ReportInputException("report schema, run ID, and audit run ID must agree");
        }
    }

    private static string Percent(double value) =>
        (value * 100).ToString("0.##", CultureInfo.InvariantCulture) + "%";

    private static string Number<T>(T? value) where T : struct =>
        value?.ToString() ?? "missing";

    private static string Duration(TimeSpan? value) =>
        value?.ToString("c", CultureInfo.InvariantCulture) ?? "missing";
}
