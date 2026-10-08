using Wharf.Run;
using System.Text.Json;

namespace Wharf.Tests;

public sealed class RunServiceRetryTests
{
    [Theory]
    [InlineData("failed", "OutOfMemory")]
    [InlineData("failed", "AgentTimeout")]
    [InlineData("failed", "VerifierTimeout")]
    [InlineData("failed", "VerifierInfrastructure")]
    public void RetriesExceptionalNonPassingTrials(string status, string errorType)
    {
        Assert.True(RunService.ShouldRetryTrial(status, errorType, 0, 1));
    }

    [Theory]
    [InlineData("passed", "OutOfMemory", 0)]
    [InlineData("failed", null, 0)]
    [InlineData("failed", "Verifier", 1)]
    [InlineData("failed", "Invocation", 0)]
    public void DoesNotRetryPassingOrdinaryOrExhaustedTrials(
        string status,
        string? errorType,
        int completedRetries)
    {
        Assert.False(RunService.ShouldRetryTrial(status, errorType, completedRetries, 1));
    }

    [Fact]
    public void ArchivesFailedExecutionBeforeFreshRetry()
    {
        string trialDirectory = Path.Combine(
            Path.GetTempPath(),
            $"wharf-trial-retry-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(trialDirectory, "agent"));
            Directory.CreateDirectory(Path.Combine(trialDirectory, "workspace"));
            File.WriteAllText(Path.Combine(trialDirectory, "agent", "patch.diff"), "patch");
            File.WriteAllText(
                Path.Combine(trialDirectory, "result.json"),
                JsonSerializer.Serialize(new
                {
                    status = "failed",
                    agentWorkspace = Path.Combine(trialDirectory, "workspace")
                }));

            RunService.ArchiveTrialExecution(trialDirectory, 1);

            string retryDirectory = Path.Combine(
                trialDirectory,
                "trial-retries",
                "retry-1");
            Assert.True(File.Exists(Path.Combine(retryDirectory, "agent", "patch.diff")));
            Assert.True(Directory.Exists(Path.Combine(retryDirectory, "workspace")));
            Assert.False(Directory.Exists(Path.Combine(trialDirectory, "agent")));
            using JsonDocument result = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(retryDirectory, "result.json")));
            Assert.Equal(
                Path.Combine(retryDirectory, "workspace"),
                result.RootElement.GetProperty("agentWorkspace").GetString());
        }
        finally
        {
            if (Directory.Exists(trialDirectory))
            {
                Directory.Delete(trialDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void CleansOnlyWorkspaceDirectories()
    {
        string trialDirectory = Path.Combine(
            Path.GetTempPath(),
            $"wharf-workspace-cleanup-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(trialDirectory, "agent"));
            Directory.CreateDirectory(Path.Combine(trialDirectory, "workspace"));
            Directory.CreateDirectory(
                Path.Combine(trialDirectory, "workspace-infrastructure-retry-1"));
            Directory.CreateDirectory(Path.Combine(trialDirectory, "trial-retries", "retry-1"));
            File.WriteAllText(Path.Combine(trialDirectory, "agent", "patch.diff"), "patch");

            RunService.CleanupWorkspaceDirectories(trialDirectory);

            Assert.False(Directory.Exists(Path.Combine(trialDirectory, "workspace")));
            Assert.False(Directory.Exists(
                Path.Combine(trialDirectory, "workspace-infrastructure-retry-1")));
            Assert.True(File.Exists(Path.Combine(trialDirectory, "agent", "patch.diff")));
            Assert.True(Directory.Exists(
                Path.Combine(trialDirectory, "trial-retries", "retry-1")));
        }
        finally
        {
            if (Directory.Exists(trialDirectory))
                Directory.Delete(trialDirectory, recursive: true);
        }
    }

    [Fact]
    public void VerifierImageCleanupArgumentsAreDistinctAndDeterministic()
    {
        IReadOnlyList<string> arguments = RunService.BuildVerifierImageCleanupArguments(
            ["task-b__verifier__:latest", "task-a__verifier__:latest", "task-b__verifier__:latest"]);

        Assert.Equal(
            [
                "image",
                "rm",
                "task-a__verifier__:latest",
                "task-b__verifier__:latest"
            ],
            arguments);
    }
}
