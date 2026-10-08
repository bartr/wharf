using System.Text.Json;
using Wharf.Run;

namespace Wharf.Tests;

public sealed class ProgressServiceTests
{
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
                Record("run=004/005 task=done-task status=complete result=passed cost=19"),
                Record("run=005/005 task=failed-task status=agent-failed result=failed")
            ]);

            RunProgress progress = ProgressService.Read(path);

            Assert.Equal(3, progress.TestsRunning);
            Assert.Equal(1, progress.Building);
            Assert.Equal(1, progress.Agents);
            Assert.Equal(1, progress.Verifiers);
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
        string? errorType = null) =>
        JsonSerializer.Serialize(new
        {
            timestamp = "2026-10-08T00:00:00Z",
            level = "info",
            message,
            error,
            errorType
        });
}
