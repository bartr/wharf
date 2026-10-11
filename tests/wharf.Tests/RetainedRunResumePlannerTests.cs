using System.Text.Json;
using Wharf.Recovery;
using Wharf.Tasks;

namespace Wharf.Tests;

public sealed class RetainedRunResumePlannerTests : IDisposable
{
    private static string FixtureRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/harbor"));

    private readonly string temporaryRoot = Path.Combine(
        Path.GetTempPath(),
        $"wharf-retained-resume-{Guid.NewGuid():N}");

    public void Dispose() => Directory.Delete(temporaryRoot, recursive: true);

    [Fact]
    public void GivenChangedTaskChecksum_WhenPlanning_RejectsRetainedIdentity()
    {
        RetainedRunFixture fixture = CreateRun(k: 1);
        HarborTask changed = fixture.Tasks[0];
        Directory.CreateDirectory(Path.Combine(
            fixture.RunDirectory,
            $"{changed.TaskId}__deadbeef"));

        RecoveryException exception = Assert.Throws<RecoveryException>(
            () => new RetainedRunResumePlanner().Plan(
                fixture.RunDirectory,
                FixtureRoot));

        Assert.Contains("checksum mismatch", exception.Message);
    }

    [Fact]
    public void GivenPartialAgentEvidence_WhenPlanning_PreservesRetryAndAllCosts()
    {
        RetainedRunFixture fixture = CreateRun(k: 2);
        TaskAttempt pending = fixture.Attempts[^1];
        CompleteAllExcept(fixture, pending);
        string trialDirectory = Path.Combine(
            fixture.RunDirectory,
            RetainedRunResumePlanner.TrialDirectoryName(pending, fixture.K));
        Directory.CreateDirectory(Path.Combine(
            trialDirectory,
            "trial-retries",
            "retry-1"));
        WriteJson(
            Path.Combine(trialDirectory, "trial-retries", "retry-1", "result.json"),
            new { totalCost = 2.5m });
        Directory.CreateDirectory(Path.Combine(
            trialDirectory,
            "agent",
            "infrastructure-retries",
            "retry-a"));
        WriteJson(
            Path.Combine(trialDirectory, "agent", "result.json"),
            new { cost = 1.25m });
        WriteJson(
            Path.Combine(
                trialDirectory,
                "agent",
                "infrastructure-retries",
                "retry-a",
                "result.json"),
            new { cost = 0.5m });

        RetainedResumePlan plan = new RetainedRunResumePlanner().Plan(
            fixture.RunDirectory,
            FixtureRoot);

        RetainedResumeAttempt resumed = Assert.Single(plan.Attempts);
        Assert.Equal(1, resumed.TrialRetry);
        Assert.Equal(4.25m, resumed.TotalCost);
    }

    [Fact]
    public void GivenRetainedRun_WhenPlanning_SelectsOnlyMissingAttempts()
    {
        RetainedRunFixture fixture = CreateRun(k: 2);
        TaskAttempt pending = fixture.Attempts[^1];
        CompleteAllExcept(fixture, pending);

        RetainedResumePlan plan = new RetainedRunResumePlanner().Plan(
            fixture.RunDirectory,
            FixtureRoot,
            n: 8,
            p: 12);

        RetainedResumeAttempt resumed = Assert.Single(plan.Attempts);
        Assert.Equal(pending.AttemptId, resumed.Attempt.AttemptId);
        Assert.Equal(fixture.Attempts.Count - 1, plan.CompletedCount);
        Assert.Equal(8, plan.N);
        Assert.Equal(8, plan.P);
        Assert.Equal("test-model", plan.Model);
        Assert.Equal("max", plan.Effort);
        Assert.Equal("1.0.95", plan.CliVersion);
    }

    private static void CompleteAllExcept(
        RetainedRunFixture fixture,
        TaskAttempt pending)
    {
        foreach (TaskAttempt attempt in fixture.Attempts.Where(
                     attempt => attempt.AttemptId != pending.AttemptId))
        {
            string trialDirectory = Path.Combine(
                fixture.RunDirectory,
                RetainedRunResumePlanner.TrialDirectoryName(attempt, fixture.K));
            Directory.CreateDirectory(trialDirectory);
            WriteJson(
                Path.Combine(trialDirectory, "result.json"),
                new { taskRoot = FixtureRoot });
        }
    }

    [Fact]
    public void GivenTrialEvidence_WhenArchiving_PreservesRetryDirectories()
    {
        Directory.CreateDirectory(temporaryRoot);
        string trialDirectory = Path.Combine(temporaryRoot, "trial");
        Directory.CreateDirectory(Path.Combine(trialDirectory, "agent"));
        Directory.CreateDirectory(Path.Combine(trialDirectory, "trial-retries", "retry-1"));
        Directory.CreateDirectory(Path.Combine(trialDirectory, "verifier-retries", "retry-1"));
        File.WriteAllText(Path.Combine(trialDirectory, "artifacts.txt"), "partial");

        string archived = ResumeEvidenceArchiver.Archive(trialDirectory);

        Assert.True(Directory.Exists(Path.Combine(archived, "agent")));
        Assert.True(File.Exists(Path.Combine(archived, "artifacts.txt")));
        Assert.True(Directory.Exists(Path.Combine(trialDirectory, "trial-retries", "retry-1")));
        Assert.True(Directory.Exists(Path.Combine(trialDirectory, "verifier-retries", "retry-1")));
    }

    private RetainedRunFixture CreateRun(int k)
    {
        string runDirectory = Path.Combine(
            temporaryRoot,
            "copilot-test-model-max-1.0.95",
            "all-1");
        Directory.CreateDirectory(runDirectory);
        IReadOnlyList<HarborTask> tasks = new HarborTaskLoader().Load(FixtureRoot);
        IReadOnlyList<TaskAttempt> attempts = TaskSelector.ExpandAttempts(tasks, k);
        WriteJson(
            Path.Combine(runDirectory, "run.jsonl"),
            new
            {
                message =
                    $"plan tasks={tasks.Count} attempts={k} runs={attempts.Count} " +
                    "task-concurrency=4 preparation-concurrency=2 " +
                    "admission-order=longest-first"
            });
        return new RetainedRunFixture(runDirectory, k, tasks, attempts);
    }

    private static void WriteJson(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value));
    }

    private sealed record RetainedRunFixture(
        string RunDirectory,
        int K,
        IReadOnlyList<HarborTask> Tasks,
        IReadOnlyList<TaskAttempt> Attempts);
}
