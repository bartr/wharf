using System.Text.Json;
using Wharf.Audit;
using Wharf.Reporting;

namespace Wharf.Tests;

public sealed class ReportRendererTests
{
    [Fact]
    public void RendersIdenticalMarkdownAndMachineOutput()
    {
        ReportInput input = CreateInput();
        ReportRenderer renderer = new();

        Assert.Equal(renderer.RenderMarkdown(input), renderer.RenderMarkdown(input));
        Assert.Equal(renderer.RenderJson(input), renderer.RenderJson(input));
        Assert.Single(renderer.RenderJsonLines(input).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void IncludesProvenanceAndOperationalCompleteness()
    {
        ReportInput input = CreateInput();
        string json = new ReportRenderer().RenderJson(input);

        Assert.Contains("\"provenance\"", json);
        Assert.Contains("\"rateLimits\"", json);
        Assert.Contains("\"completeness\"", json);
        Assert.Contains("HTTP 429 responses", new ReportRenderer().RenderMarkdown(input));
    }

    private static ReportInput CreateInput()
    {
        AuditAttempt attempt = new(
            "attempt-1", 1,
            Measurement<bool>.ValidValue(true),
            Measurement<double>.ValidValue(1),
            Measurement<bool>.ValidValue(false),
            Measurement<double>.ValidValue(1.5),
            Measurement<long>.ValidValue(2),
            Measurement<long>.ValidValue(3),
            Measurement<long>.ValidValue(4),
            Measurement<long>.ValidValue(1),
            Measurement<long>.ValidValue(5),
            Measurement<TimeSpan>.ValidValue(TimeSpan.FromSeconds(1)),
            Measurement<TimeSpan>.ValidValue(TimeSpan.FromSeconds(2)));
        AuditReport audit = new AuditScorer().Score(
            new AuditRun("run-1", ["task-1"], [new("task-1", [attempt])]));
        return new ReportInput(
            ReportSchema.Version,
            "run-1",
            JsonDocument.Parse("""{"model":"fixture"}""").RootElement,
            new SortedDictionary<string, string> { ["source"] = "fixture.json" },
            audit,
            new RetryReport(1, 0, 0),
            new RateLimitReport(2, 1, 0, 250));
    }
}
