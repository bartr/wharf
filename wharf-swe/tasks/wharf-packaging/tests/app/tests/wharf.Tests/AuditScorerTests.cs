using Wharf.Audit;

namespace Wharf.Tests;

public sealed class AuditScorerTests
{
    [Fact]
    public void ScoresPassAtOneAndTaskEqualEnterpriseScore()
    {
        AuditAttempt passing = Attempt(1, passed: true, coverage: 0.5);
        AuditAttempt partial = Attempt(1, passed: false, coverage: 0.25);
        AuditAttempt full = Attempt(2, passed: true, coverage: 1);
        AuditReport report = new AuditScorer().Score(new AuditRun(
            "run-1",
            ["task-a", "task-b", "task-c"],
            [new("task-a", [passing]), new("task-b", [partial, full])]));

        Assert.Equal(1d / 3, report.PassAt1, 10);
        Assert.Equal(40, report.EnterpriseScore, 10);
        Assert.Equal(1, report.Completeness.MissingTasks);
        Assert.False(report.Completeness.IsComplete);
    }

    [Fact]
    public void KeepsMissingInvalidPartialAndZeroVisible()
    {
        AuditAttempt attempt = Attempt(1, passed: false, coverage: 0);
        attempt = attempt with
        {
            Cost = Measurement<double>.MissingValue(),
            InputTokens = Measurement<long>.InvalidValue(),
            OutputTokens = Measurement<long>.PartialValue(4),
            CacheTokens = Measurement<long>.ZeroValue()
        };

        AuditReport report = new AuditScorer().Score(new AuditRun("run-2", ["task"], [new("task", [attempt])]));

        Assert.Equal(0, report.EnterpriseScore);
        Assert.Equal(1, report.Completeness.MissingMeasurements);
        Assert.Equal(1, report.Completeness.InvalidMeasurements);
        Assert.Equal(1, report.Completeness.PartialMeasurements);
        Assert.Equal(1, report.Completeness.ZeroMeasurements);
        Assert.Null(report.Cost);
        Assert.Null(report.InputTokens);
    }

    [Fact]
    public void ExistingRegressionZerosAttemptScore()
    {
        AuditAttempt attempt = Attempt(1, passed: true, coverage: 1) with
        {
            ExistingTestRegression = Measurement<bool>.ValidValue(true)
        };

        AuditReport report = new AuditScorer().Score(new AuditRun("run-3", ["task"], [new("task", [attempt])]));

        Assert.Equal(1, report.PassAt1);
        Assert.Equal(0, report.EnterpriseScore);
        Assert.True(report.ExistingTestRegression);
    }

    private static AuditAttempt Attempt(int number, bool passed, double coverage) =>
        new(
            $"attempt-{number}", number,
            Measurement<bool>.ValidValue(passed),
            Measurement<double>.ValidValue(coverage),
            Measurement<bool>.ValidValue(false),
            Measurement<double>.ValidValue(2),
            Measurement<long>.ValidValue(10),
            Measurement<long>.ValidValue(20),
            Measurement<long>.ValidValue(3),
            Measurement<long>.ValidValue(1),
            Measurement<long>.ValidValue(4),
            Measurement<TimeSpan>.ValidValue(TimeSpan.FromSeconds(1)),
            Measurement<TimeSpan>.ValidValue(TimeSpan.FromSeconds(2)));
}
