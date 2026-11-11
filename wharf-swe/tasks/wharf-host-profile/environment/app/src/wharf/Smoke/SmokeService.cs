using System.Text.Json;
using Wharf.Audit;
using Wharf.Host;
using Wharf.Reporting;

namespace Wharf.Smoke;

public sealed record SmokeResult(
    HostCheckReport Host,
    string Status,
    string ArtifactDirectory,
    string ReportPath,
    string TrialPath,
    int ExitCode);

public sealed class SmokeService
{
    public SmokeResult Run(string outputDirectory, HostCheckReport host)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new SmokeException("smoke output directory is required");

        string root = Path.GetFullPath(outputDirectory);
        string source = Path.Combine(root, "source");
        string workspace = Path.Combine(root, "agent-workspace");
        string verifier = Path.Combine(root, "verifier-workspace");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(verifier);

        File.WriteAllText(Path.Combine(source, "smoke.txt"), "wharf smoke fixture\n");
        File.WriteAllText(Path.Combine(workspace, "smoke.patch"), "fixture patch applied\n");
        File.Copy(Path.Combine(source, "smoke.txt"), Path.Combine(verifier, "smoke.txt"), overwrite: true);
        File.AppendAllText(Path.Combine(verifier, "smoke.txt"), "verified\n");

        string trialPath = Path.Combine(root, "trial.json");
        File.WriteAllText(trialPath, JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            taskId = "wharf-smoke",
            status = "completed",
            agentWorkspace = workspace,
            verifierWorkspace = verifier,
            artifact = Path.Combine(root, "smoke.txt")
        }, JsonOptions));
        File.WriteAllText(Path.Combine(root, "smoke.txt"), File.ReadAllText(Path.Combine(verifier, "smoke.txt")));

        AuditAttempt attempt = new(
            "smoke-attempt-1", 1,
            Measurement<bool>.ValidValue(true),
            Measurement<double>.ValidValue(1),
            Measurement<bool>.ValidValue(false),
            Measurement<double>.ZeroValue(),
            Measurement<long>.ZeroValue(),
            Measurement<long>.ZeroValue(),
            Measurement<long>.ZeroValue(),
            Measurement<long>.ZeroValue(),
            Measurement<long>.ZeroValue(),
            Measurement<TimeSpan>.ValidValue(TimeSpan.Zero),
            Measurement<TimeSpan>.ValidValue(TimeSpan.Zero));
        AuditReport audit = new AuditScorer().Score(
            new AuditRun("wharf-smoke", ["wharf-smoke"], [new("wharf-smoke", [attempt])]));
        ReportInput input = new(
            ReportSchema.Version,
            "wharf-smoke",
            JsonDocument.Parse("""{"mode":"local-fixture","task":"wharf-smoke"}""").RootElement,
            new SortedDictionary<string, string>
            {
                ["fixture"] = "local",
                ["source"] = "SmokeService"
            },
            audit,
            new RetryReport(0, 0, 0),
            new RateLimitReport(0, 0, 0, 0));
        ReportRenderer renderer = new();
        string reportPath = Path.Combine(root, "report.md");
        File.WriteAllText(reportPath, renderer.RenderMarkdown(input));
        File.WriteAllText(Path.Combine(root, "report.json"), renderer.RenderJson(input) + Environment.NewLine);
        File.WriteAllText(Path.Combine(root, "report.jsonl"), renderer.RenderJsonLines(input));
        File.WriteAllText(Path.Combine(root, "host-check.json"), JsonSerializer.Serialize(host, JsonOptions));

        string status = host.IsSuccess ? "completed" : "qualification-blocked";
        return new SmokeResult(host, status, root, reportPath, trialPath,
            host.IsSuccess ? 0 : host.ExitCode);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}

public sealed class SmokeException(string message) : Exception(message);
