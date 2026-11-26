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
    [InlineData("", 0)]
    [InlineData("--help", 0)]
    [InlineData("--quiet", 0)]
    [InlineData("version", 0)]
    [InlineData("run --dry-run --output-format json", 0)]
    [InlineData("run --dry-run -n 2", 0)]
    [InlineData("prebuild --help", 0)]
    [InlineData("smoke --help", 0)]
    [InlineData("resume --help", 0)]
    [InlineData("analytics manifest --help", 0)]
    [InlineData("analytics export --help", 0)]
    [InlineData("--unknown-flag", 1)]
    [InlineData("unknown-command", 1)]
    public async Task EveryInvocationInitializesMissingUserConfig(string arguments, int expectedExit)
    {
        var result = await InvokeWithUserConfig(null,
            arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal(expectedExit, result.ExitCode);
    }

    [Fact]
    public async Task DryRunAutomaticallyLoadsUserConfigAndHonorsExplicitOverrides()
    {
        const string json = """
            {"k":3,"n":8,"p":6,"retainWorkspaces":true,"shortestFirst":true,
             "timeouts":{"agentSeconds":123},"retries":{"maxAttempts":2}}
            """;
        var baseline = await InvokeWithUserConfig(json, "run", "--dry-run", "--output-format", "json");
        Assert.Equal(0, baseline.ExitCode);
        using JsonDocument defaults = JsonDocument.Parse(baseline.Output);
        Assert.Equal(3, defaults.RootElement.GetProperty("k").GetInt32());
        Assert.Equal(6, defaults.RootElement.GetProperty("p").GetInt32());
        Assert.True(defaults.RootElement.GetProperty("retainWorkspaces").GetBoolean());
        Assert.Equal(123, defaults.RootElement.GetProperty("timeouts").GetProperty("agentSeconds").GetInt32());

        var overridden = await InvokeWithUserConfig(json, "run", "--dry-run",
            "--output-format", "json", "-n", "2", "--retain-workspaces", "false",
            "--shortest-first", "false", "--cli", "codex", "--cli-version", "0.169.0");
        Assert.Equal(0, overridden.ExitCode);
        using JsonDocument flags = JsonDocument.Parse(overridden.Output);
        Assert.Equal(2, flags.RootElement.GetProperty("n").GetInt32());
        Assert.Equal(2, flags.RootElement.GetProperty("p").GetInt32());
        Assert.False(flags.RootElement.GetProperty("retainWorkspaces").GetBoolean());
        Assert.False(flags.RootElement.GetProperty("shortestFirst").GetBoolean());
        Assert.Equal("0.169.0", flags.RootElement.GetProperty("cliVersion").GetString());
        Assert.Equal("codex", flags.RootElement.GetProperty("cli").GetString());
    }

    [Theory]
    [InlineData("run", "--dry-run")]
    [InlineData("run", "--no-op")]
    [InlineData("prebuild", "--verbose")]
    [InlineData("smoke", "--allow-unsupported-host")]
    public async Task InvalidUserConfigFailsBeforeCreatingOutput(string command, string flag)
    {
        var result = await InvokeWithUserConfig("""{"unknown":true}""", command, flag);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("invalid user configuration", result.Error);
    }

    [Fact]
    public async Task RemovedCpuUserConfigFailsWithoutRewritingIt()
    {
        var result = await InvokeWithUserConfig("""{"containerCpus":8}""",
            "run", "--dry-run");
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("invalid user configuration", result.Error);
        Assert.Contains("containerCpus", result.Error);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("stop")]
    [InlineData("report")]
    [InlineData("profile")]
    public async Task RemovedCommandsFailAsUnknownWithoutCreatingRunEvidence(string command)
    {
        var result = await InvokeWithUserConfig(null, command);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains(command, result.Error);
        Assert.DoesNotContain("not implemented", result.Error);

        RootCommand root = CommandTree.Create(new WharfConfiguration());
        Assert.DoesNotContain(root.Subcommands, registered => registered.Name == command);
        var help = await InvokeWithUserConfig(null, "--help");
        Assert.Equal(0, help.ExitCode);
        Assert.DoesNotContain($"{command} ", help.Output);
    }

    [Fact]
    public async Task AnalyticsExportDefaultsToJobsAndAcceptsFriendlyFlags()
    {
        RootCommand root = CommandTree.Create(new WharfConfiguration());
        Assert.Empty(root.Parse(["analytics", "export", "--recompute", "--output-directory", "data"]).Errors);
        var result = await InvokeWithUserConfig(null, "analytics", "export");
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("jobs", result.Error);
        Assert.DoesNotContain("analytics output", result.Error);
        Assert.DoesNotContain("model is required", result.Error);
    }

    internal static async Task<(int ExitCode, string Output, string Error)> InvokeWithUserConfig(
        string? json, params string[] arguments)
    {
        string home = Path.Combine(Path.GetTempPath(), $"wharf-user-home-{Guid.NewGuid():N}");
        Directory.CreateDirectory(home);
        string path = Path.Combine(home, ".wharf", "config.json");
        if (json is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
        }
        try
        {
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo("dotnet")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WorkingDirectory = home
                }
            };
            process.StartInfo.Environment["HOME"] = home;
            foreach (string name in RunConfigurationResolver.ReadEnvironment().Keys)
                process.StartInfo.Environment.Remove(name);
            process.StartInfo.ArgumentList.Add(typeof(WharfApp).Assembly.Location);
            foreach (string argument in arguments)
                process.StartInfo.ArgumentList.Add(argument);
            Assert.True(process.Start());
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            string output = await stdout;
            string error = await stderr;
            Assert.False(Directory.Exists(Path.Combine(home, "jobs")));
            Assert.False(File.Exists(Path.Combine(home, "run.jsonl")));
            if (json is not null)
                Assert.Equal(json, File.ReadAllText(path));
            else
            {
                Assert.True(File.Exists(path));
                RunConfiguration expected = RunConfigurationResolver.Resolve(
                    null, new RunConfigurationOverrides(), new Dictionary<string, string?>());
                RunConfiguration initialized = RunConfigurationResolver.Resolve(
                    UserConfigurationLoader.Load(home),
                    new RunConfigurationOverrides(), new Dictionary<string, string?>());
                Assert.Equal(RunConfigurationRenderer.Render(expected, "json"),
                    RunConfigurationRenderer.Render(initialized, "json"));
                Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
            }
            return (process.ExitCode, output, error);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public async Task RunRejectsRemovedManifestOptionBeforeCreatingOutput()
    {
        RootCommand root = CommandTree.Create(new WharfConfiguration());
        var command = root.Subcommands.Single(command => command.Name == "run");
        Assert.DoesNotContain(command.Options, option => option.Name == "--manifest");
        Assert.NotEmpty(root.Parse(["run", "--manifest", "old.json", "--dry-run"]).Errors);
        string output = $"wharf-removed-manifest-{Guid.NewGuid():N}";
        int exit = await WharfApp.RunAsync([
            "run", "--manifest", "old.json", "--output-directory", output]);
        Assert.Equal(1, exit);
        Assert.False(Directory.Exists(output));
        Assert.Empty(root.Parse(["analytics", "manifest", "--help"]).Errors);
    }

    [Theory]
    [InlineData("--no-op")]
    [InlineData("--oracle")]
    public void RunAcceptsBaselineFlagsWithoutChangingDryRun(string flag)
    {
        RootCommand root = CommandTree.Create(new WharfConfiguration());
        var command = root.Subcommands.Single(command => command.Name == "run");
        Option<bool> option = Assert.IsType<Option<bool>>(
            command.Options.Single(option => option.Name == flag));
        var result = root.Parse(["run", flag, "--dry-run"]);
        Assert.Empty(result.Errors);
        Assert.True(result.GetValue(option));
    }

    [Fact]
    public void RunRejectsRemovedContainerCpuOption()
    {
        RootCommand root = CommandTree.Create(new WharfConfiguration());
        var command = root.Subcommands.Single(command => command.Name == "run");
        Assert.DoesNotContain(command.Options, option => option.Name == "--container-cpus");

        var result = root.Parse(["run", "--container-cpus", "12", "--dry-run"]);

        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task RemovedContainerCpuFlagFailsBeforeCreatingRunOutput()
    {
        string output = $"wharf-invalid-cpu-{Guid.NewGuid():N}";

        int exit = await WharfApp.RunAsync([
            "run", "--container-cpus", "0", "--output-directory", output
        ]);

        Assert.Equal(1, exit);
        Assert.False(Directory.Exists(output));
    }

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
    [InlineData("prebuild", "-p")]
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
    public void PrebuildAcceptsExactCliVersion()
    {
        RootCommand root = CommandTree.Create(new WharfConfiguration());
        var command = root.Subcommands.Single(command => command.Name == "prebuild");
        Option<string> option = Assert.IsType<Option<string>>(
            command.Options.Single(option => option.Name == "--cli-version"));
        var result = root.Parse(["prebuild", "-p", "12", "--cli-version", "1.0.107"]);
        Assert.Empty(result.Errors);
        Assert.Equal("1.0.107", result.GetValue(option));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task InvalidPrebuildConcurrencyFailsBeforeImagePreparation(string value)
    {
        int exit = await WharfApp.RunAsync(["prebuild", "-p", value]);
        Assert.Equal(2, exit);
    }

    [Fact]
    public void PrebuildReplacesBuildAndDoesNotAcceptTrialConcurrency()
    {
        RootCommand root = CommandTree.Create(new WharfConfiguration());
        Assert.DoesNotContain(root.Subcommands, command => command.Name == "build");
        Assert.NotEmpty(root.Parse(["build"]).Errors);
        Assert.NotEmpty(root.Parse(["prebuild", "-n", "4"]).Errors);
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
                "--tasks", Path.Combine(FindRepositoryRoot(), "deep-swe", "smoke-tasks"),
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
               !Directory.Exists(Path.Combine(directory.FullName, "deep-swe", "smoke-tasks")))
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
