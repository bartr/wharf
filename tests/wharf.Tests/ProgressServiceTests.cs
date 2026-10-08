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

    private static string Record(string message) =>
        JsonSerializer.Serialize(new
        {
            timestamp = "2026-10-08T00:00:00Z",
            level = "info",
            message
        });
}
