using System.Text.Json;

namespace Wharf.Reporting;

public static class ReportSchema
{
    public const string Version = "1.1";
}

public sealed record RetryReport(
    int InfrastructureRetries,
    int RetryableFailures,
    int ExhaustedRetries);

public sealed record RateLimitReport(
    int Responses429,
    int AffectedImages,
    int ExhaustedImages,
    long CumulativeDelayMilliseconds);

public sealed record ReportInput(
    string SchemaVersion,
    string RunId,
    JsonElement Configuration,
    IReadOnlyDictionary<string, string> Provenance,
    Wharf.Audit.AuditReport Audit,
    RetryReport Retries,
    RateLimitReport RateLimits);

public sealed class ReportInputException(string message) : Exception(message);
