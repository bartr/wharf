using Wharf;
using System.Diagnostics;
using System.Text.Json;
using System.CommandLine;
using Wharf.Command;
using Wharf.Configuration;
using Wharf.Versioning;

namespace Wharf.Tests;

public sealed class WharfAppTests
{
    [Theory]
    [InlineData(false, "--model", "-m", "custom-model")]
    [InlineData(false, "--effort", "-e", "high")]
    [InlineData(true, "--model", "-m", "custom-model")]
    [InlineData(true, "--effort", "-e", "high")]
    public void ModelAndEffortAcceptLongAndShortOptions(
        bool analytics, string longName, string shortName, string value)
    {
        RootCommand root = CommandTree.Create(new WharfConfiguration());
        string[] prefix = analytics ? ["analytics", "export"] : ["run"];
        var command = root.Subcommands.Single(command =>
            command.Name == (analytics ? "analytics" : "run"));
        if (analytics)
            command = command.Subcommands.Single(command => command.Name == "export");
        Option<string> option = Assert.IsType<Option<string>>(
            command.Options.Single(option => option.Name == longName));

        foreach (string name in new[] { longName, shortName })
        {
            var result = root.Parse([.. prefix, name, value]);

            Assert.Empty(result.Errors);
            Assert.Equal(value, result.GetValue(option));
        }
    }

    [Theory]
    [InlineData("run", "-k")]
    [InlineData("run", "-n")]
    [InlineData("run", "-p")]
    [InlineData("resume", "-n")]
    [InlineData("resume", "-p")]
    public void ConcurrencyOptionsUseSingleDash(string commandName, string optionName)
    {
        RootCommand root = CommandTree.Create(new WharfConfiguration());
        var command = root.Subcommands.Single(command => command.Name == commandName);
        Option<int?> option = Assert.IsType<Option<int?>>(
            command.Options.Single(option => option.Name == optionName));

        var result = root.Parse([commandName, optionName, "3"]);

        Assert.Empty(result.Errors);
        Assert.Equal(3, result.GetValue(option));
        Assert.NotEmpty(root.Parse([commandName, "-" + optionName, "3"]).Errors);
    }

    [Fact]
    public async Task VersionCommandPrintsOnlySemanticVersion()
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        process.StartInfo.ArgumentList.Add(typeof(WharfApp).Assembly.Location);
        process.StartInfo.ArgumentList.Add("version");

        Assert.True(process.Start());
        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.Equal(0, process.ExitCode);
        Assert.Equal(WharfVersion.Current + Environment.NewLine, stdout);
        Assert.Empty(stderr);
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
            string agentDirectory = Path.Combine(trialDirectory, "agent");
            Assert.NotEmpty(File.ReadAllText(Path.Combine(agentDirectory, "stdout.log")));
            Assert.True(File.Exists(Path.Combine(agentDirectory, "stderr.log")));
            Assert.NotEmpty(File.ReadAllText(Path.Combine(agentDirectory, "patch.diff")));
            Assert.True(File.Exists(Path.Combine(
                trialDirectory,
                "artifacts",
                "model.patch")));
            Assert.True(File.Exists(Path.Combine(
                trialDirectory,
                "verifier",
                "reward.json")));
            Assert.Contains("started=", File.ReadLines(runLog).First());
            Assert.Contains(
                File.ReadLines(runLog),
                line => line.Contains("status=verifier-image-cleaned", StringComparison.Ordinal));
            Assert.Contains(
                File.ReadLines(runLog),
                line => line.Contains("status=agent-image-cleaned", StringComparison.Ordinal));
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
