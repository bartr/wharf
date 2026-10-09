using Wharf.Run;
using System.Text.Json;
using Wharf.Verifier;

namespace Wharf.Tests;

public sealed class RunServiceRetryTests
{
    [Theory]
    [InlineData("failed", "OutOfMemory")]
    [InlineData("failed", "AgentTimeout")]
    public void RetriesExceptionalNonPassingTrials(string status, string errorType)
    {
        Assert.True(RunService.ShouldRetryTrial(status, errorType, 0, 1));
    }

    [Theory]
    [InlineData("passed", "OutOfMemory", 0)]
    [InlineData("failed", null, 0)]
    [InlineData("failed", "Verifier", 1)]
    [InlineData("failed", "Invocation", 0)]
    [InlineData("failed", "VerifierTimeout", 0)]
    [InlineData("failed", "VerifierInfrastructure", 0)]
    public void DoesNotRetryPassingOrdinaryOrExhaustedTrials(
        string status,
        string? errorType,
        int completedRetries)
    {
        Assert.False(RunService.ShouldRetryTrial(status, errorType, completedRetries, 1));
    }

    [Theory]
    [InlineData("VerifierTimeout")]
    [InlineData("VerifierInfrastructure")]
    public void RetriesVerifierInfrastructureWithoutRetryingWholeTrial(string errorType)
    {
        Assert.True(RunService.ShouldRetryVerifier(errorType, 0, 1));
        Assert.False(RunService.ShouldRetryTrial("failed", errorType, 0, 1));
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("Verifier", 0)]
    [InlineData("AgentTimeout", 0)]
    [InlineData("VerifierTimeout", 1)]
    public void DoesNotRetryDeterministicOrExhaustedVerifierFailures(
        string? errorType,
        int completedRetries)
    {
        Assert.False(RunService.ShouldRetryVerifier(errorType, completedRetries, 1));
    }

    [Fact]
    public async Task RetriesVerifierCallbackAndReturnsSuccessfulReplacement()
    {
        int executions = 0;
        int archives = 0;
        TrialResult expected = PassingTrial();

        VerifierRetryOutcome outcome = await RunService.ExecuteVerifierWithRetryAsync(
            (_, _) =>
            {
                executions++;
                return executions == 1
                    ? Task.FromException<TrialResult>(
                        new VerifierExecutionException("timed out", "VerifierTimeout"))
                    : Task.FromResult(expected);
            },
            (retry, exception, _) =>
            {
                archives++;
                Assert.Equal(1, retry);
                Assert.Equal("VerifierTimeout", exception.ErrorType);
                return Task.CompletedTask;
            },
            maximumRetries: 1,
            backoffSeconds: 0,
            CancellationToken.None);

        Assert.Same(expected, outcome.Result);
        Assert.Equal(1, outcome.RetryCount);
        Assert.Equal(2, executions);
        Assert.Equal(1, archives);
    }

    [Fact]
    public void ArchivesOnlyFailedVerifierEvidence()
    {
        string trialDirectory = Path.Combine(
            Path.GetTempPath(),
            $"wharf-verifier-retry-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(trialDirectory, "agent"));
            Directory.CreateDirectory(Path.Combine(trialDirectory, "artifacts"));
            Directory.CreateDirectory(Path.Combine(trialDirectory, "verifier"));
            File.WriteAllText(Path.Combine(trialDirectory, "agent", "patch.diff"), "patch");
            File.WriteAllText(Path.Combine(trialDirectory, "artifacts", "model.patch"), "patch");
            File.WriteAllText(Path.Combine(trialDirectory, "verifier", "trial-result.json"), "{}");

            RunService.ArchiveVerifierExecution(trialDirectory, 1);

            Assert.False(Directory.Exists(Path.Combine(trialDirectory, "verifier")));
            Assert.True(File.Exists(Path.Combine(trialDirectory, "agent", "patch.diff")));
            Assert.True(File.Exists(Path.Combine(trialDirectory, "artifacts", "model.patch")));
            Assert.True(File.Exists(Path.Combine(
                trialDirectory,
                "verifier-retries",
                "retry-1",
                "trial-result.json")));
        }
        finally
        {
            if (Directory.Exists(trialDirectory))
                Directory.Delete(trialDirectory, recursive: true);
        }
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
    public void CleansOnlyAgentWorkingDirectories()
    {
        string trialDirectory = Path.Combine(
            Path.GetTempPath(),
            $"wharf-workspace-cleanup-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(trialDirectory, "agent"));
            Directory.CreateDirectory(Path.Combine(trialDirectory, "workspace"));
            Directory.CreateDirectory(Path.Combine(trialDirectory, "workspace-home"));
            Directory.CreateDirectory(
                Path.Combine(trialDirectory, "workspace-infrastructure-retry-1"));
            Directory.CreateDirectory(
                Path.Combine(trialDirectory, "workspace-infrastructure-retry-1-home"));
            Directory.CreateDirectory(Path.Combine(trialDirectory, "trial-retries", "retry-1"));
            File.WriteAllText(Path.Combine(trialDirectory, "agent", "patch.diff"), "patch");

            RunService.CleanupWorkspaceDirectories(trialDirectory);

            Assert.False(Directory.Exists(Path.Combine(trialDirectory, "workspace")));
            Assert.False(Directory.Exists(Path.Combine(trialDirectory, "workspace-home")));
            Assert.False(Directory.Exists(
                Path.Combine(trialDirectory, "workspace-infrastructure-retry-1")));
            Assert.False(Directory.Exists(
                Path.Combine(trialDirectory, "workspace-infrastructure-retry-1-home")));
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
    public void ManagedImageNamesUseRolePrefixes()
    {
        Assert.Equal(
            "agent-task__run:latest",
            RunService.BuildAgentImageName("task__run"));
        Assert.Equal(
            "verifier-task_name__run:latest",
            RunService.BuildVerifierImageName("task/name", "run"));
    }

    [Fact]
    public void VerifierImageCleanupArgumentsAreDistinctAndDeterministic()
    {
        IReadOnlyList<string> arguments = RunService.BuildVerifierImageCleanupArguments(
            ["verifier-task-b:latest", "verifier-task-a:latest", "verifier-task-b:latest"]);

        Assert.Equal(
            [
                "image",
                "rm",
                "verifier-task-a:latest",
                "verifier-task-b:latest"
            ],
            arguments);
    }

    private static TrialResult PassingTrial() =>
        new(
            TrialResultSchema.Version,
            "task",
            "passed",
            "/tmp/workspace",
            [],
            "/tmp/patch.diff");
}
