using System.CommandLine;
using Wharf.Command;
using Wharf.Configuration;
using Wharf.Host;
using Wharf.Logging;
using Wharf.Smoke;

namespace Wharf.Tests;

public sealed class SmokeServiceTests
{
    [Theory]
    [InlineData("run", "1.2.3")]
    [InlineData("smoke", "1.2.3")]
    [InlineData("prebuild", "1.2.3")]
    [InlineData("run", "latest")]
    [InlineData("smoke", "latest")]
    [InlineData("prebuild", "latest")]
    public void CommandsAcceptCliVersion(string name, string version)
    {
        RootCommand root = CommandTree.Create(new WharfConfiguration());
        var command = root.Subcommands.Single(item => item.Name == name);
        var option = Assert.IsType<Option<string?>>(
            command.Options.Single(item => item.Name == "--cli-version"));
        var parsed = root.Parse([name, "--cli-version", version]);

        Assert.Empty(parsed.Errors);
        Assert.Equal(version, parsed.GetValue(option));
    }

    [Theory]
    [InlineData("smoke", "--model", "--effort")]
    [InlineData("smoke", "-m", "-e")]
    [InlineData("run", "--model", "--effort")]
    [InlineData("run", "-m", "-e")]
    public void CommandsAcceptModelAndEffort(string name, string modelFlag, string effortFlag)
    {
        RootCommand root = CommandTree.Create(new WharfConfiguration());
        var command = root.Subcommands.Single(item => item.Name == name);
        var model = Assert.IsType<Option<string?>>(
            command.Options.Single(item => item.Name == "--model"));
        var effort = Assert.IsType<Option<string?>>(
            command.Options.Single(item => item.Name == "--effort"));
        var parsed = root.Parse([name, modelFlag, "custom-model", effortFlag, "high"]);

        Assert.Empty(parsed.Errors);
        Assert.Equal("custom-model", parsed.GetValue(model));
        Assert.Equal("high", parsed.GetValue(effort));
    }

    [Theory]
    [InlineData("copilot")]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ExplicitModelAndEffortOverrideEnvironment(string cli)
    {
        RunConfiguration configuration = SmokeService.ResolveConfiguration(
            Path.GetTempPath(), cli, "custom-model", "max",
            environment: new Dictionary<string, string?>
            {
                ["MODEL"] = "short-environment-model",
                ["WHARF_MODEL"] = "environment-model",
                ["WHARF_EFFORT"] = "low"
            });

        Assert.Equal("custom-model", configuration.Model);
        Assert.Equal("max", configuration.Effort);
        Assert.Equal(Path.GetTempPath(), configuration.OutputDirectory);
    }

    [Theory]
    [InlineData("copilot", "1.0.108")]
    [InlineData("claude", "2.1.241")]
    [InlineData("codex", "0.169.0")]
    public void SmokeUsesSelectedProviderVersionPin(string cli, string version)
    {
        RunConfiguration configuration = SmokeService.ResolveConfiguration(
            "jobs", cli, cliVersion: version,
            environment: new Dictionary<string, string?>());

        Assert.Equal(version, configuration.CliVersion);
    }

    [Fact]
    public void OmittedFlagsPreserveEnvironmentModelAndEffort()
    {
        RunConfiguration configuration = SmokeService.ResolveConfiguration(
            "jobs", environment: new Dictionary<string, string?>
            {
                ["WHARF_MODEL"] = "environment-model",
                ["WHARF_EFFORT"] = "high"
            });

        Assert.Equal("environment-model", configuration.Model);
        Assert.Equal("high", configuration.Effort);
    }

    [Theory]
    [InlineData("copilot", "hydrafusion")]
    [InlineData("claude", "claude-sonnet-5-5")]
    [InlineData("codex", "gpt-6-luna")]
    public void OmittedFlagsPreserveProviderDefaults(string cli, string model)
    {
        RunConfiguration configuration = SmokeService.ResolveConfiguration(
            "jobs", cli, environment: new Dictionary<string, string?>());

        Assert.Equal(model, configuration.Model);
        Assert.Equal("default", configuration.Effort);
    }

    [Fact]
    public void UnsupportedEffortFailsBeforeExecution()
    {
        RunConfigurationException exception = Assert.Throws<RunConfigurationException>(() =>
            SmokeService.ResolveConfiguration("jobs", effort: "invalid",
                environment: new Dictionary<string, string?>()));

        Assert.Contains("unsupported effort: invalid", exception.Message);
    }

    [Fact]
    public void PreservesHostQualificationFailureInResult()
    {
        string directory = Path.Combine(Path.GetTempPath(), "wharf-smoke-" + Guid.NewGuid().ToString("N"));
        try
        {
            HostCheckReport host = new([
                new HostCheck("platform", true, true, "supported"),
                new HostCheck("docker", false, false, "daemon unavailable")
            ]);
            using WharfLogger logger = WharfLogger.Create(new());
            SmokeResult result = new SmokeService().Run(
                Path.Combine(FindRepositoryRoot(), "deep-swe", "smoke-tasks"),
                directory,
                host,
                logger);

            Assert.Equal("qualification-blocked", result.Status);
            Assert.Equal(4, result.ExitCode);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RejectsMissingSmokeTaskDirectory()
    {
        using WharfLogger logger = WharfLogger.Create(new());
        HostCheckReport host = new([]);

        Assert.Throws<SmokeException>(() => new SmokeService().Run(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            Path.GetTempPath(),
            host,
            logger));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !Directory.Exists(Path.Combine(directory.FullName, "deep-swe", "smoke-tasks")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("repository root not found");
    }
}
