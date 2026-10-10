using System.Text.Json;
using Wharf.Command;
using Wharf.Run;

namespace Wharf.Tests;

public sealed class ProgressServiceTests
{
    [Fact]
    public void SeparatesTerminalCompletionFromMissingGraderCoverage()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"wharf-progress-{Guid.NewGuid():N}.jsonl");
        try
        {
            File.WriteAllLines(path,
            [
                Record(
                    "run=001/002 task=no-grader status=complete result=agent-failed",
                    "agent execution exceeded its timeout",
                    "AgentTimeout"),
                Record("run=002/002 task=graded status=verifier-complete result=passed pass@1=1"),
                Record("run=002/002 task=graded status=complete result=passed")
            ]);

            RunProgress progress = ProgressService.Read(path);

            Assert.Equal(2, progress.TestsCompleted);
            Assert.Equal(1, progress.PassAtTotal);
            Assert.Equal(50m, progress.PassPercent);
            Assert.Equal(1, progress.MissingGraderResults);
            Assert.Equal(1, progress.MissingGraderTypes!["AgentTimeout"]);
            Assert.Equal(1, progress.Timeouts);
            Assert.Equal(0, progress.TestsRunning);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DerivesQueuedCountAndWaitAggregatesFromStructuredTransitions()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"wharf-progress-{Guid.NewGuid():N}.jsonl");
        try
        {
            File.WriteAllLines(path,
            [
                Record("run=001/002 task=queued status=waiting-preparation attempt=2/2"),
                PreparationRecord(1, "queued", "agent", "waiting", 2, 2, 1, 0),
                Record("run=002/002 task=active status=waiting-preparation attempt=2/2"),
                PreparationRecord(2, "active", "verifier", "waiting", 2, 2, 0, 1),
                PreparationRecord(2, "active", "verifier", "acquired", 2, 2, 0, 1, 120),
                PreparationRecord(3, "cancelled", "agent-retry", "waiting", 1, 1, 1, 2),
                PreparationRecord(3, "cancelled", "agent-retry", "cancelled", 1, 1, 1, 2, 60)
            ]);

            RunProgress progress = ProgressService.Read(path);

            Assert.Equal(1, progress.Queued);
            Assert.Equal(1, progress.TestsRunning);
            Assert.Equal(1, progress.Building);
            Assert.Equal(0, progress.TestsCompleted);
            Assert.Collection(
                progress.PreparationWaits!,
                aggregate =>
                {
                    Assert.Equal("agent-retry", aggregate.Operation);
                    Assert.Equal(1, aggregate.K);
                    Assert.Equal(1, aggregate.Count);
                    Assert.Equal(60, aggregate.TotalWaitMilliseconds);
                    Assert.Equal(60m, aggregate.AverageWaitMilliseconds);
                    Assert.Equal(60, aggregate.MaximumWaitMilliseconds);
                },
                aggregate =>
                {
                    Assert.Equal("verifier", aggregate.Operation);
                    Assert.Equal(2, aggregate.K);
                    Assert.Equal(1, aggregate.Count);
                    Assert.Equal(120, aggregate.TotalWaitMilliseconds);
                });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReportsActiveTrialsByLifecycleStage()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"wharf-progress-{Guid.NewGuid():N}.jsonl");
        try
        {
            File.WriteAllLines(path,
            [
                Record("run=001/004 task=build-task status=agent image checking base"),
                Record("run=002/004 task=agent-task status=running-agent"),
                Record("run=003/004 task=verifier-task status=preparing-verifier-image"),
                Record("run=004/005 task=done-task status=verifier-complete result=passed pass@1=1"),
                Record("run=004/005 task=done-task status=complete result=passed cost=19"),
                Record(
                    "run=005/005 task=failed-task status=complete result=failed",
                    "agent execution failed",
                    "AgentExecution")
            ]);

            RunProgress progress = ProgressService.Read(path);

            Assert.Equal(3, progress.TestsRunning);
            Assert.Equal(1, progress.Building);
            Assert.Equal(1, progress.Agents);
            Assert.Equal(1, progress.Verifiers);
            Assert.Equal(0, progress.Cleanup);
            Assert.Equal(2, progress.TestsCompleted);
            Assert.Equal(0, progress.FailedTests);
            Assert.Equal(0, progress.Timeouts);
            Assert.Equal(1, progress.Errors);
            Assert.Equal(1, progress.PassAtTotal);
            Assert.Equal(50m, progress.PassPercent);
            Assert.Equal(0.19m, progress.AverageCost);
            Assert.Equal(0.19m, progress.TotalCost);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CountsInfrastructureRetryAsActiveAgentWork()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"wharf-progress-{Guid.NewGuid():N}.jsonl");
        try
        {
            File.WriteAllLines(path,
            [
                Record("run=001/001 task=retrying-task status=running-agent"),
                Record(
                    "run=001/001 task=retrying-task status=agent-infrastructure-retry " +
                    "retry=1 reason=copilot-service")
            ]);

            RunProgress progress = ProgressService.Read(path);

            Assert.Equal(1, progress.TestsRunning);
            Assert.Equal(0, progress.Building);
            Assert.Equal(1, progress.Agents);
            Assert.Equal(0, progress.Verifiers);
            Assert.Equal(0, progress.Cleanup);
            Assert.Equal(1, progress.Retries);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReportsCleanupAsActiveLifecycleOccupancy()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"wharf-progress-{Guid.NewGuid():N}.jsonl");
        try
        {
            File.WriteAllLines(path,
            [
                Record("run=001/002 task=image-cleanup status=verifier-image-cleaned"),
                Record("run=002/002 task=workspace-cleanup status=cleaning-up")
            ]);

            RunProgress progress = ProgressService.Read(path);

            Assert.Equal(2, progress.TestsRunning);
            Assert.Equal(0, progress.Building);
            Assert.Equal(0, progress.Agents);
            Assert.Equal(0, progress.Verifiers);
            Assert.Equal(2, progress.Cleanup);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CleanupAfterFailedCompletionDoesNotResurrectTrial()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"wharf-progress-{Guid.NewGuid():N}.jsonl");
        try
        {
            File.WriteAllLines(path,
            [
                Record("run=001/001 task=failed-task status=running-verifier"),
                Record("run=001/001 task=failed-task status=verifier-complete result=failed"),
                Record("run=001/001 task=failed-task status=cleaning-up"),
                Record("run=001/001 task=failed-task status=complete result=failed"),
                Record(
                    "run=001/001 task=failed-task status=agent-image-cleaned " +
                    "image=agent-failed-task:latest")
            ]);

            RunProgress progress = ProgressService.Read(path);

            Assert.Equal(0, progress.TestsRunning);
            Assert.Equal(1, progress.TestsCompleted);
            Assert.Equal(1, progress.FailedTests);
            Assert.Empty(progress.ActiveTrials);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CountsTrialRetryBackoffAsBuildingOccupancy()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"wharf-progress-{Guid.NewGuid():N}.jsonl");
        try
        {
            File.WriteAllLines(path,
            [
                Record(
                    "run=001/001 task=retry-task status=agent-complete result=failed",
                    "agent container reported a cgroup OOM kill",
                    "OutOfMemory"),
                Record("run=001/001 task=retry-task status=trial-retry retry=1 reason=OutOfMemory")
            ]);

            RunProgress progress = ProgressService.Read(path);

            Assert.Equal(1, progress.TestsRunning);
            Assert.Equal(1, progress.Building);
            Assert.Equal(0, progress.Agents);
            Assert.Equal(0, progress.Verifiers);
            Assert.Equal(0, progress.Cleanup);
            Assert.Equal(1, progress.Retries);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CountsVerifierRetryAsActiveVerifierWork()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"wharf-progress-{Guid.NewGuid():N}.jsonl");
        try
        {
            File.WriteAllLines(path,
            [
                Record(
                    "run=001/001 task=retry-task status=verifier-failed result=failed",
                    "verifier command exceeded its timeout",
                    "VerifierTimeout"),
                Record(
                    "run=001/001 task=retry-task status=verifier-retry " +
                    "retry=1 reason=VerifierTimeout")
            ]);

            RunProgress progress = ProgressService.Read(path);

            Assert.Equal(1, progress.TestsRunning);
            Assert.Equal(1, progress.Verifiers);
            Assert.Equal(0, progress.Errors);
            Assert.Equal(1, progress.Retries);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ShowsPerKLinesOnlyAfterSecondAttemptStarts()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"wharf-progress-{Guid.NewGuid():N}.jsonl");
        try
        {
            File.WriteAllLines(path,
            [
                Record(
                    "run=001/002 task=first-task attempt=1/2 K=2 " +
                    "status=complete result=passed cost=10")
            ]);
            RunProgress firstAttemptOnly = ProgressService.Read(path);

            Assert.DoesNotContain(
                CommandTree.FormatProgress(firstAttemptOnly),
                line => line.StartsWith("k=", StringComparison.Ordinal));

            File.AppendAllLines(path,
            [
                Record(
                    "run=002/002 task=second-task attempt=2/2 K=2 " +
                    "status=running-agent")
            ]);
            RunProgress secondAttemptStarted = ProgressService.Read(path);
            string[] lines = CommandTree.FormatProgress(secondAttemptStarted).ToArray();

            Assert.Contains(lines, line => line.StartsWith("k=1 completed=", StringComparison.Ordinal));
            Assert.Contains(lines, line => line.StartsWith("k=2 completed=", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FormatsTerminalActiveAndCostSummariesOnSeparateLines()
    {
        var progress = new RunProgress(
            1,
            1,
            0,
            1,
            0,
            0,
            2,
            1,
            0,
            0,
            new Dictionary<string, int>(),
            1,
            50m,
            new Dictionary<int, ProgressGroup>(),
            0.25m,
            0.50m,
            []);

        IReadOnlyList<string> lines = CommandTree.FormatProgress(progress);

        Assert.Equal(
            "completed=2 pass-rate=50.00% pass@=1 failed=1 errors=0 timeouts=0 retries=0 missing-grader=0",
            lines[0]);
        Assert.Equal(
            "queued=0 running=1 building=0 agent=1 verifier=0 cleanup=0",
            lines[1]);
        Assert.Equal("avg-cost=0.25 total-cost=0.50", lines[2]);
    }

    [Fact]
    public void VerboseProgressOrdersActiveTrialsByStageDuration()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"wharf-progress-{Guid.NewGuid():N}.jsonl");
        try
        {
            File.WriteAllLines(path,
            [
                Record(
                    "run=001/002 task=short-task status=running-agent",
                    timestamp: "2026-10-08T12:09:00Z"),
                Record(
                    "run=002/002 task=long-task status=waiting-preparation",
                    timestamp: "2026-10-08T12:00:00Z"),
                Record(
                    "run=002/002 task=long-task status=running-agent",
                    timestamp: "2026-10-08T12:05:00Z")
            ]);

            RunProgress progress = ProgressService.Read(
                path,
                DateTimeOffset.Parse("2026-10-08T12:10:00Z"));
            IReadOnlyList<string> lines = CommandTree.FormatProgress(progress, verbose: true);
            Assert.Equal(string.Empty, lines[3]);
            string[] details = lines.Skip(4).ToArray();

            Assert.Equal(
                "00:05:00 agent long-task",
                details[0]);
            Assert.Equal(
                "00:01:00 agent short-task",
                details[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void VerboseProgressAlignsNamesAfterLongestStage()
    {
        var progress = new RunProgress(
            1,
            2,
            0,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            new Dictionary<string, int>(),
            0,
            null,
            new Dictionary<int, ProgressGroup>(),
            null,
            null,
            [
                new ActiveTrialProgress("agent-task", "agent", 120_000),
                new ActiveTrialProgress("verifier-task", "verifier", 60_000)
            ]);

        IReadOnlyList<string> lines = CommandTree.FormatProgress(progress, verbose: true);
        Assert.Equal(string.Empty, lines[3]);
        string[] details = lines.Skip(4).ToArray();

        Assert.Equal("00:02:00 agent    agent-task", details[0]);
        Assert.Equal("00:01:00 verifier verifier-task", details[1]);
        Assert.Equal(details[0].IndexOf("agent-task"), details[1].IndexOf("verifier-task"));
    }

    [Fact]
    public void VerboseProgressUsesFullTaskNameFromStructuredStageEvent()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"wharf-progress-{Guid.NewGuid():N}.jsonl");
        try
        {
            File.WriteAllLines(path,
            [
                JsonSerializer.Serialize(new
                {
                    timestamp = "2026-10-08T12:00:00Z",
                    level = "info",
                    message =
                        "run=001/002 task=full-task-name-beyond-human-prefix " +
                        "stage=agent transition=started attempt=1 retry=0",
                    eventType = "stage",
                    runNumber = 1,
                    taskId = "full-task-name-beyond-human-prefix"
                }),
                Record(
                    "run=001/002 task=full-task-name-beyond-human-pref status=running-agent",
                    timestamp: "2026-10-08T12:00:00Z")
            ]);

            RunProgress progress = ProgressService.Read(
                path,
                DateTimeOffset.Parse("2026-10-08T12:01:00Z"));

            ActiveTrialProgress active = Assert.Single(progress.ActiveTrials);
            Assert.Equal("full-task-name-beyond-human-prefix", active.Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TerminalVerifierPassSupersedesAgentInfrastructureFailure()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"wharf-progress-{Guid.NewGuid():N}.jsonl");
        try
        {
            File.WriteAllLines(path,
            [
                Record(
                    "run=001/001 task=oom-task status=agent-complete result=failed",
                    "agent container reported a cgroup OOM kill",
                    "OutOfMemory"),
                Record("run=001/001 task=oom-task status=running-verifier"),
                Record("run=001/001 task=oom-task status=verifier-complete result=passed pass@1=1"),
                Record("run=001/001 task=oom-task status=complete result=passed")
            ]);

            RunProgress progress = ProgressService.Read(path);

            Assert.Equal(1, progress.TestsCompleted);
            Assert.Equal(0, progress.FailedTests);
            Assert.Equal(0, progress.Timeouts);
            Assert.Equal(0, progress.Errors);
            Assert.Equal(1, progress.PassAtTotal);
            Assert.Equal(100m, progress.PassPercent);
            Assert.Empty(progress.ErrorTypes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FinalFailureRetainsUnderlyingAgentFailureType()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"wharf-progress-{Guid.NewGuid():N}.jsonl");
        try
        {
            File.WriteAllLines(path,
            [
                Record(
                    "run=001/001 task=timeout-task status=agent-complete result=failed",
                    "agent execution exceeded its timeout",
                    "AgentTimeout"),
                Record("run=001/001 task=timeout-task status=running-verifier"),
                Record(
                    "run=001/001 task=timeout-task status=verifier-complete result=failed pass@1=0",
                    "verifier command failed or reported reward 0",
                    "Verifier"),
                Record("run=001/001 task=timeout-task status=complete result=failed")
            ]);

            RunProgress progress = ProgressService.Read(path);

            Assert.Equal(1, progress.TestsCompleted);
            Assert.Equal(0, progress.FailedTests);
            Assert.Equal(1, progress.Timeouts);
            Assert.Equal(0, progress.Errors);
            Assert.Empty(progress.ErrorTypes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RewardZeroIsAFailedTestNotAnError()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"wharf-progress-{Guid.NewGuid():N}.jsonl");
        try
        {
            File.WriteAllLines(path,
            [
                Record("run=001/001 task=failed-test status=running-verifier"),
                Record("run=001/001 task=failed-test status=verifier-complete result=failed pass@1=0"),
                Record("run=001/001 task=failed-test status=complete result=failed")
            ]);

            RunProgress progress = ProgressService.Read(path);

            Assert.Equal(1, progress.TestsCompleted);
            Assert.Equal(1, progress.FailedTests);
            Assert.Equal(0, progress.Timeouts);
            Assert.Equal(0, progress.Errors);
            Assert.Equal(0, progress.PassAtTotal);
            Assert.Equal(0m, progress.PassPercent);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TrialRetryClearsTheEarlierExecutionError()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"wharf-progress-{Guid.NewGuid():N}.jsonl");
        try
        {
            File.WriteAllLines(path,
            [
                Record(
                    "run=001/001 task=retry-task status=agent-complete result=failed cost=10",
                    "agent container reported a cgroup OOM kill",
                    "OutOfMemory"),
                Record("run=001/001 task=retry-task status=complete result=failed"),
                Record("run=001/001 task=retry-task status=trial-retry retry=1 reason=OutOfMemory"),
                Record("run=001/001 task=retry-task status=agent-complete result=completed cost=25"),
                Record("run=001/001 task=retry-task status=verifier-complete result=failed pass@1=0"),
                Record("run=001/001 task=retry-task status=complete result=failed")
            ]);

            RunProgress progress = ProgressService.Read(path);

            Assert.Equal(1, progress.TestsCompleted);
            Assert.Equal(1, progress.FailedTests);
            Assert.Equal(0, progress.Timeouts);
            Assert.Equal(0, progress.Errors);
            Assert.Null(progress.TotalCost);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PassingTrialRetryReportsCumulativeCost()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"wharf-progress-{Guid.NewGuid():N}.jsonl");
        try
        {
            File.WriteAllLines(path,
            [
                Record(
                    "run=001/001 task=retry-task status=agent-complete result=failed cost=10",
                    "agent execution exceeded its timeout",
                    "AgentTimeout"),
                Record("run=001/001 task=retry-task status=complete result=failed"),
                Record("run=001/001 task=retry-task status=trial-retry retry=1 reason=AgentTimeout"),
                Record("run=001/001 task=retry-task status=agent-complete result=completed cost=25"),
                Record("run=001/001 task=retry-task status=verifier-complete result=passed pass@1=1"),
                Record("run=001/001 task=retry-task status=complete result=passed")
            ]);

            RunProgress progress = ProgressService.Read(path);

            Assert.Equal(1, progress.PassAtTotal);
            Assert.Equal(0, progress.Timeouts);
            Assert.Equal(0, progress.Errors);
            Assert.Equal(0.25m, progress.TotalCost);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string Record(
        string message,
        string? error = null,
        string? errorType = null,
        string timestamp = "2026-10-08T00:00:00Z") =>
        JsonSerializer.Serialize(new
        {
            timestamp,
            level = "info",
            message,
            error,
            errorType
        });

    private static string PreparationRecord(
        int runNumber,
        string taskId,
        string operation,
        string transition,
        int logicalAttempt,
        int k,
        int trialRetry,
        int operationRetry,
        long? waitMilliseconds = null) =>
        JsonSerializer.Serialize(new
        {
            timestamp = "2026-10-08T00:00:00Z",
            level = "info",
            message = $"preparation-gate operation={operation} transition={transition}",
            eventType = "preparation-gate",
            runNumber,
            taskId,
            operation,
            transition,
            logicalAttempt,
            trialRetry,
            operationRetry,
            k,
            waitMilliseconds
        });
}
