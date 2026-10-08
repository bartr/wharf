using Wharf.Tasks;

namespace Wharf.Tests;

public sealed class HarborTaskTests
{
    private static string FixtureRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/harbor"));

    [Fact]
    public void LoadsSanitizedHarborFixturesAndComputesChecksums()
    {
        IReadOnlyList<HarborTask> tasks = new HarborTaskLoader().Load(FixtureRoot);

        Assert.Equal(5, tasks.Count);
        Assert.Contains(tasks, task => task.TaskId == "smoke-go");
        Assert.All(tasks, task =>
        {
            Assert.Equal("1.3", task.SchemaVersion);
            Assert.False(string.IsNullOrWhiteSpace(task.SourceChecksum));
            Assert.True(File.Exists(task.InstructionPath));
            Assert.Equal(1800, task.AgentBuildTimeoutSeconds);
            Assert.True(task.AgentTimeoutSeconds > 0);
            Assert.Equal(1800, task.VerifierBuildTimeoutSeconds);
            Assert.Equal(1800, task.VerifierTimeoutSeconds);
            Assert.DoesNotContain("solution", Directory.EnumerateFiles(task.RootPath, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(task.RootPath, path)),
                StringComparer.OrdinalIgnoreCase);
        });
        Assert.Equal(
            1800,
            tasks.Single(task => task.TaskId == "smoke-go").AgentTimeoutSeconds);
        Assert.Equal(
            42.5,
            tasks.Single(task => task.TaskId == "smoke-go").EstimatedAgentDurationSeconds);
    }

    [Fact]
    public void SelectionIsDeterministicAndSupportsFiltersAndAttempts()
    {
        IReadOnlyList<HarborTask> tasks = new HarborTaskLoader().Load(FixtureRoot);
        var selection = new TaskSelection(Language: "go", Sample: 1, Seed: 42, Attempts: 3);

        IReadOnlyList<HarborTask> first = TaskSelector.Select(tasks, selection);
        IReadOnlyList<HarborTask> second = TaskSelector.Select(tasks, selection);
        IReadOnlyList<TaskAttempt> attempts = TaskSelector.ExpandAttempts(first, selection.Attempts);

        Assert.Equal(first.Select(task => task.TaskId), second.Select(task => task.TaskId));
        Assert.Equal(3, attempts.Count);
        Assert.Equal("attempt-1", attempts[0].AttemptId.Split('#')[1]);
        Assert.All(attempts, attempt => Assert.Equal(first[0].TaskId, attempt.Task.TaskId));
    }

    [Fact]
    public void AdmissionOrderCompletesEachKWaveAndStartsLongestTasksFirst()
    {
        IReadOnlyList<HarborTask> tasks = new HarborTaskLoader().Load(FixtureRoot);
        HarborTask smoke = tasks.Single(task => task.TaskId == "smoke-go");
        HarborTask shortTask = tasks.First(task => task.TaskId != "smoke-go") with
        {
            EstimatedAgentDurationSeconds = 10
        };

        IReadOnlyList<TaskAttempt> ordered = TaskSelector.OrderAttemptsForAdmission(
            TaskSelector.ExpandAttempts([shortTask, smoke], 2));

        Assert.Equal(
            [
                $"{smoke.TaskId}#attempt-1",
                $"{shortTask.TaskId}#attempt-1",
                $"{smoke.TaskId}#attempt-2",
                $"{shortTask.TaskId}#attempt-2"
            ],
            ordered.Select(attempt => attempt.AttemptId));
    }

    [Fact]
    public void IncludeExcludeAndShardAreAppliedAfterStableOrdering()
    {
        IReadOnlyList<HarborTask> tasks = new HarborTaskLoader().Load(FixtureRoot);
        IReadOnlyList<HarborTask> included = TaskSelector.Select(
            tasks,
            new TaskSelection(Include: "feature_request", Exclude: "python", Shard: "0/2"));

        Assert.NotEmpty(included);
        Assert.All(included, task =>
        {
            Assert.Equal("feature_request", task.Category);
            Assert.NotEqual("python", task.Language);
        });
    }

    [Fact]
    public void UnsupportedSchemaFailsExplicitly()
    {
        string root = CreateTemporaryTaskRoot();
        try
        {
            string manifest = Path.Combine(root, "task.toml");
            File.WriteAllText(
                manifest,
                """
                schema_version = "9.9"
                """);

            HarborTaskException exception = Assert.Throws<HarborTaskException>(
                () => new HarborTaskLoader().Load(Path.GetDirectoryName(root)!));

            Assert.Contains("unsupported task schema version", exception.Message);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(root)!, recursive: true);
        }
    }

    private static string CreateTemporaryTaskRoot()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"wharf-task-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string taskDirectory = Path.Combine(directory, "invalid");
        Directory.CreateDirectory(taskDirectory);
        File.WriteAllText(Path.Combine(taskDirectory, "instruction.md"), "invalid");
        return taskDirectory;
    }
}
