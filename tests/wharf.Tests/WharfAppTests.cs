using Wharf;
using System.Text.Json;

namespace Wharf.Tests;

public sealed class WharfAppTests
{
    [Fact]
    public async Task VersionCommandSucceeds()
    {
        int exitCode = await WharfApp.RunAsync(["version"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunCommandExecutesTaskDiscovery()
    {
        string relativeOutput = "run-test";
        string outputDirectory = Path.GetFullPath(relativeOutput);
        try
        {
            int exitCode = await WharfApp.RunAsync([
                "run",
                "-v",
                "--tasks", Path.Combine(FindRepositoryRoot(), "smoke-tasks"),
                "--output-directory", relativeOutput
            ]);

            Assert.Equal(0, exitCode);
            string runLog = Assert.Single(Directory.GetFiles(
                outputDirectory,
                "run.jsonl",
                SearchOption.AllDirectories));
            string runDirectory = Path.GetDirectoryName(runLog)!;
            string trialDirectory = Assert.Single(Directory.GetDirectories(runDirectory));
            Assert.False(Directory.Exists(Path.Combine(trialDirectory, "workspace")));
            using JsonDocument result = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(trialDirectory, "result.json")));
            Assert.False(result.RootElement.GetProperty("workspaceRetained").GetBoolean());
            Assert.Equal(JsonValueKind.Null, result.RootElement.GetProperty("agentWorkspace").ValueKind);
            Assert.Contains("started=", File.ReadLines(runLog).First());
            Assert.Contains(
                File.ReadLines(runLog),
                line => line.Contains("status=verifier-image-cleaned", StringComparison.Ordinal));
            JsonElement[] stageEvents = File.ReadLines(runLog)
                .Select(line => JsonDocument.Parse(line).RootElement.Clone())
                .Where(record =>
                    record.TryGetProperty("eventType", out JsonElement eventType) &&
                    eventType.GetString() == "stage")
                .ToArray();
            foreach (string stage in new[] { "preparation", "agent", "verifier", "cleanup" })
            {
                Assert.Contains(stageEvents, record =>
                    record.GetProperty("stage").GetString() == stage &&
                    record.GetProperty("transition").GetString() == "started");
                JsonElement completed = Assert.Single(stageEvents, record =>
                    record.GetProperty("stage").GetString() == stage &&
                    record.GetProperty("transition").GetString() == "completed");
                Assert.Equal("smoke-go", completed.GetProperty("taskId").GetString());
                Assert.True(completed.GetProperty("durationMilliseconds").GetInt64() >= 0);
            }
        }

        finally
        {
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !Directory.Exists(Path.Combine(directory.FullName, "smoke-tasks")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("repository root not found");
    }

    [Theory]
    [InlineData("-v")]
    [InlineData("-vv")]
    [InlineData("-vvv")]
    public async Task RepeatableVerbosityFormsAreAccepted(string option)
    {
        int exitCode = await WharfApp.RunAsync([option]);

        Assert.Equal(0, exitCode);
    }
}
