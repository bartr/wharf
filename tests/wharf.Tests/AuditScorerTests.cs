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

        Assert.Equal(2d / 3, report.PassAt1, 10);
        Assert.Equal(2, report.PassingAttempts);
        Assert.Equal(3, report.ObservedAttempts);
        Assert.Equal(1, report.TaskPassAtLeastOnce);
        Assert.Equal(2, report.PassingTasks);
        Assert.Equal(2, report.ObservedTasks);
        Assert.Equal(40, report.EnterpriseScore, 10);
        Assert.Equal(30, report.EnterpriseCoverageComponent, 10);
        Assert.Equal(10, report.EnterprisePassComponent, 10);
        Assert.Equal(
            report.EnterpriseScore,
            report.EnterpriseCoverageComponent + report.EnterprisePassComponent,
            10);
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
        Assert.Equal(1, report.TaskPassAtLeastOnce);
        Assert.Equal(0, report.EnterpriseScore);
        Assert.True(report.ExistingTestRegression);
    }

    [Fact]
    public void PassAtOneUsesObservedLogicalAttemptsRatherThanExpectedTasks()
    {
        AuditReport report = new AuditScorer().Score(new AuditRun(
            "run-4",
            ["task-a", "task-b", "missing"],
            [
                new("task-a", [Attempt(1, true, 1), Attempt(2, false, 0)]),
                new("task-b", [Attempt(1, false, 0)])
            ]));

        Assert.Equal(1d / 3, report.PassAt1, 10);
        Assert.Equal(1d / 2, report.TaskPassAtLeastOnce, 10);
        Assert.Equal(1, report.PassingAttempts);
        Assert.Equal(3, report.ObservedAttempts);
    }

    [Fact]
    public void RejectsDuplicateObservedTasks()
    {
        AuditRun run = new(
            "run-5",
            ["task"],
            [new("task", [Attempt(1, true, 1)]), new("task", [Attempt(2, true, 1)])]);

        Assert.Throws<AuditContractException>(() => new AuditScorer().Score(run));
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
