using System.Text.Json;
using Wharf.Configuration;

namespace Wharf.Tests;

public sealed class RunConfigurationTests
{
    [Fact]
    public async Task ConcurrentInitializationPreservesDefaultsAndExistingFiles()
    {
        string home = Path.Combine(Path.GetTempPath(), $"wharf-init-{Guid.NewGuid():N}");
        string directory = Path.Combine(home, ".wharf");
        Directory.CreateDirectory(directory);
        string credential = Path.Combine(directory, "copilot");
        File.WriteAllText(credential, "synthetic-credential");
        try
        {
            await Task.WhenAll(Enumerable.Range(0, 12).Select(_ =>
                Task.Run(() => UserConfigurationLoader.EnsureExists(home))));
            string path = Path.Combine(directory, "config.json");
            Assert.NotNull(UserConfigurationLoader.Load(home).K);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
            File.WriteAllText(path, """{"k":7}""");
            UserConfigurationLoader.EnsureExists(home);
            Assert.Equal("""{"k":7}""", File.ReadAllText(path));
            Assert.Equal("synthetic-credential", File.ReadAllText(credential));
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void InitializationFailureIsExplicitAndPreservesObstructingFile()
    {
        string home = Path.Combine(Path.GetTempPath(), $"wharf-init-{Guid.NewGuid():N}");
        Directory.CreateDirectory(home);
        string path = Path.Combine(home, ".wharf");
        File.WriteAllText(path, "not-a-directory");
        try
        {
            RunConfigurationException exception = Assert.Throws<RunConfigurationException>(
                () => UserConfigurationLoader.EnsureExists(home));
            Assert.Contains("cannot initialize user configuration", exception.Message);
            Assert.Equal("not-a-directory", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void BaselineModesAreRetainedAndRendered(bool noOp, bool oracle)
    {
        RunConfiguration configuration = RunConfigurationResolver.Resolve(
            null, new RunConfigurationOverrides(NoOp: noOp, Oracle: oracle), new Dictionary<string, string?>());
        Assert.Equal(noOp, configuration.NoOp);
        Assert.Equal(oracle, configuration.Oracle);
        using JsonDocument document = JsonDocument.Parse(RunConfigurationRenderer.Render(configuration, "json"));
        Assert.Equal(noOp, document.RootElement.GetProperty("noOp").GetBoolean());
        Assert.Equal(oracle, document.RootElement.GetProperty("oracle").GetBoolean());
        RunConfiguration overridden = RunConfigurationResolver.Resolve(
            null, new RunConfigurationOverrides(NoOp: !noOp, Oracle: !oracle),
            new Dictionary<string, string?>());
        Assert.Equal(!noOp, overridden.NoOp);
        Assert.Equal(!oracle, overridden.Oracle);
    }

    [Fact]
    public void PrecedenceIsFlagThenEnvironmentThenUserConfigThenDefault()
    {
        var defaults = new UserConfiguration(
            K: 3,
            N: 2,
            P: 1);
        var environment = new Dictionary<string, string?>
        {
            ["WHARF_K"] = "5",
            ["WHARF_N"] = "4",
            ["WHARF_MODEL"] = "environment-model"
        };

        RunConfiguration result = RunConfigurationResolver.Resolve(
            defaults,
            new RunConfigurationOverrides(K: 7, CliVersion: "flag-version"),
            environment);

        Assert.Equal(7, result.K);
        Assert.Equal(4, result.N);
        Assert.Equal(1, result.P);
        Assert.Equal("environment-model", result.Model);
        Assert.Equal("default", result.Effort);
        Assert.Equal("flag-version", result.CliVersion);
    }

    [Theory]
    [InlineData("copilot", "1.0.108")]
    [InlineData("claude", "2.1.241")]
    [InlineData("codex", "0.169.0")]
    public void CliVersionUsesFlagThenProviderLatest(
        string cli, string flag)
    {
        var environment = new Dictionary<string, string?>();
        Assert.Equal(flag, RunConfigurationResolver.Resolve(null,
            new RunConfigurationOverrides(Cli: cli, CliVersion: flag), environment).CliVersion);
        Assert.Equal("latest", RunConfigurationResolver.Resolve(null,
            new RunConfigurationOverrides(Cli: cli), environment).CliVersion);
    }

    [Theory]
    [InlineData("copilot", "WHARF_CLI_VERSION")]
    [InlineData("claude", "WHARF_CLI_VERSION")]
    [InlineData("codex", "WHARF_CLI_VERSION")]
    [InlineData("copilot", "CLI_VERSION")]
    [InlineData("claude", "CLI_VERSION")]
    [InlineData("codex", "CLI_VERSION")]
    public void ObsoleteVersionEnvironmentFailsEvenWithExplicitPin(string cli, string variable)
    {
        RunConfigurationException exception = Assert.Throws<RunConfigurationException>(() =>
            RunConfigurationResolver.Resolve(
                null,
                new RunConfigurationOverrides(Cli: cli, CliVersion: "1.2.3"),
                new Dictionary<string, string?> { [variable] = "1.0.108" }));

        Assert.Contains(variable, exception.Message);
        Assert.Contains("unset", exception.Message);
        Assert.Contains("--cli-version", exception.Message);
    }

    [Fact]
    public void EffortUsesFlagBeforeEnvironment()
    {
        RunConfiguration result = RunConfigurationResolver.Resolve(
            null,
            new RunConfigurationOverrides(Model: "custom-model", Effort: "high"),
            new Dictionary<string, string?>
            {
                ["WHARF_EFFORT"] = "medium"
            });

        Assert.Equal("high", result.Effort);
    }

    [Fact]
    public void EffortUsesEnvironmentBeforeDefault()
    {
        RunConfiguration result = RunConfigurationResolver.Resolve(
            null,
            new RunConfigurationOverrides(Model: "custom-model"),
            new Dictionary<string, string?>
            {
                ["WHARF_EFFORT"] = "medium"
            });

        Assert.Equal("medium", result.Effort);
    }

    [Fact]
    public void UsesHydraFusionWithModelDefaultEffortWhenNotConfigured()
    {
        RunConfiguration result = RunConfigurationResolver.Resolve(
            null,
            new RunConfigurationOverrides(),
            new Dictionary<string, string?>());

        Assert.Equal(1, result.K);
        Assert.Equal("hydrafusion", result.Model);
        Assert.Equal("default", result.Effort);
        Assert.Equal("latest", result.CliVersion);
        Assert.Equal(1, result.Retries.MaxAttempts);
        Assert.False(result.RetainWorkspaces);
        Assert.False(result.ShortestFirst);
        Assert.Null(result.ContainerCpus);
    }

    [Theory]
    [InlineData(2, "4", 6, 2)]
    [InlineData(null, "4", 6, 4)]
    [InlineData(null, null, 6, 6)]
    [InlineData(null, null, null, null)]
    public void ContainerCpusUsesFlagEnvironmentUserConfigDefaultPrecedence(
        int? flag, string? environment, int? defaults, int? expected)
    {
        RunConfiguration configuration = RunConfigurationResolver.Resolve(
            new UserConfiguration(ContainerCpus: defaults),
            new RunConfigurationOverrides(ContainerCpus: flag),
            new Dictionary<string, string?> { ["WHARF_CONTAINER_CPUS"] = environment });

        Assert.Equal(expected, configuration.ContainerCpus);
        RunConfigurationValidator.Validate(configuration);
    }

    [Fact]
    public void ContainerCpuEnvironmentUsesOnlyTheDocumentedWharfName()
    {
        RunConfiguration configuration = RunConfigurationResolver.Resolve(
            null, new RunConfigurationOverrides(),
            new Dictionary<string, string?>
            {
                ["CONTAINER_CPUS"] = "2",
                ["WHARF_CONTAINER_CPUS"] = "4"
            });

        Assert.Equal(4, configuration.ContainerCpus);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unlimited")]
    [InlineData("zero")]
    [InlineData("1.5")]
    public void RejectsNonIntegerContainerCpuEnvironmentValues(string value)
    {
        Assert.Throws<RunConfigurationException>(() => RunConfigurationResolver.Resolve(
            null, new RunConfigurationOverrides(),
            new Dictionary<string, string?> { ["WHARF_CONTAINER_CPUS"] = value }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonPositiveContainerCpuLimits(int limit)
    {
        RunConfiguration configuration = RunConfigurationResolver.Resolve(
            new UserConfiguration(ContainerCpus: limit),
            new RunConfigurationOverrides(),
            new Dictionary<string, string?>());

        RunConfigurationException exception = Assert.Throws<RunConfigurationException>(() =>
            RunConfigurationValidator.Validate(configuration));
        Assert.Contains("containerCpus", exception.Message);
    }

    [Fact]
    public void RendersCpuLimitInBothDryRunFormats()
    {
        string json = DryRunService.Execute(new RunConfigurationOverrides(ContainerCpus: 12),
            "json", new Dictionary<string, string?>(), new UserConfiguration());
        string text = DryRunService.Execute(new RunConfigurationOverrides(ContainerCpus: 12),
            "text", new Dictionary<string, string?>(), new UserConfiguration());
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.Equal(12, document.RootElement.GetProperty("containerCpus").GetInt32());
        Assert.Contains("containerCpus\t12", text);
    }

    [Fact]
    public void RendersUnrestrictedCpuDefaultInBothDryRunFormats()
    {
        string json = DryRunService.Execute(new RunConfigurationOverrides(),
            "json", new Dictionary<string, string?>(), new UserConfiguration());
        string text = DryRunService.Execute(new RunConfigurationOverrides(),
            "text", new Dictionary<string, string?>(), new UserConfiguration());
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("containerCpus").ValueKind);
        Assert.Contains("containerCpus\tunrestricted", text);
    }

    [Fact]
    public void UserConfigLoadsOperationalDefaultsIntoDryRunWithoutOutput()
    {
        string home = Path.Combine(Path.GetTempPath(), $"wharf-config-{Guid.NewGuid():N}");
        string directory = Path.Combine(home, ".wharf");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "config.json"), """
            {"schemaVersion":"1.0","k":2,"n":8,"p":6,"containerCpus":12,
             "timeouts":{"setupSeconds":90,"agentSeconds":180,"verifierSeconds":60},
             "retries":{"maxAttempts":2,"backoffSeconds":3},"outputFormat":"json",
             "retainWorkspaces":true,"shortestFirst":true}
            """);
        try
        {
            UserConfiguration defaults = UserConfigurationLoader.Load(home);
            string output = $"wharf-preview-{Guid.NewGuid():N}";
            string rendered = DryRunService.Execute(new RunConfigurationOverrides(OutputDirectory: output),
                null, new Dictionary<string, string?>(), defaults);
            using JsonDocument document = JsonDocument.Parse(rendered);
            Assert.Equal(2, document.RootElement.GetProperty("k").GetInt32());
            Assert.Equal(8, document.RootElement.GetProperty("n").GetInt32());
            Assert.Equal(6, document.RootElement.GetProperty("p").GetInt32());
            Assert.Equal(12, document.RootElement.GetProperty("containerCpus").GetInt32());
            Assert.Equal(180, document.RootElement.GetProperty("timeouts").GetProperty("agentSeconds").GetInt32());
            Assert.Equal(2, document.RootElement.GetProperty("retries").GetProperty("maxAttempts").GetInt32());
            Assert.True(document.RootElement.GetProperty("retainWorkspaces").GetBoolean());
            Assert.True(document.RootElement.GetProperty("shortestFirst").GetBoolean());
            Assert.False(Directory.Exists(output));
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void ShortestFirstIsOptIn()
    {
        RunConfiguration result = RunConfigurationResolver.Resolve(
            null,
            new RunConfigurationOverrides(ShortestFirst: true),
            new Dictionary<string, string?>());

        Assert.True(result.ShortestFirst);
    }

    [Fact]
    public void ReadsHydraFusionFromShortModelEnvironmentVariable()
    {
        RunConfiguration result = RunConfigurationResolver.Resolve(
            null,
            new RunConfigurationOverrides(),
            new Dictionary<string, string?> { ["MODEL"] = "hydrafusion" });

        Assert.Equal("hydrafusion", result.Model);
        Assert.Equal("default", result.Effort);
    }

    [Fact]
    public void RejectsInvalidHydraFusionEffort()
    {
        RunConfiguration result = RunConfigurationResolver.Resolve(
            null,
            new RunConfigurationOverrides(Model: "hydrafusion", Effort: "invalid"),
            new Dictionary<string, string?>());

        RunConfigurationException exception = Assert.Throws<RunConfigurationException>(
            () => RunConfigurationValidator.Validate(result));

        Assert.Contains("effort", exception.Message);
    }

    [Fact]
    public void AcceptsDefaultEffortFromEnvironment()
    {
        RunConfiguration result = RunConfigurationResolver.Resolve(
            null,
            new RunConfigurationOverrides(),
            new Dictionary<string, string?> { ["WHARF_EFFORT"] = "default" });

        RunConfigurationValidator.Validate(result);

        Assert.Equal("default", result.Effort);
    }

    [Fact]
    public void DefaultsConcurrencyToFour()
    {
        RunConfiguration result = RunConfigurationResolver.Resolve(
            null,
            new RunConfigurationOverrides(),
            new Dictionary<string, string?>());

        Assert.Equal(4, result.N);
        Assert.Equal(4, result.P);
    }

    [Fact]
    public void AllowsWholeTrialRetriesToBeDisabled()
    {
        RunConfiguration configuration = RunConfigurationResolver.Resolve(
            new UserConfiguration(Retries: new RetryPolicy(MaxAttempts: 0)),
            new RunConfigurationOverrides(),
            new Dictionary<string, string?>());

        RunConfigurationValidator.Validate(configuration);

        Assert.Equal(0, configuration.Retries.MaxAttempts);
    }

    [Fact]
    public void RejectsUnsupportedReportTimeout()
    {
        RunConfiguration configuration = RunConfigurationResolver.Resolve(
            new UserConfiguration(Timeouts: new PhaseTimeouts(ReportSeconds: 30)),
            new RunConfigurationOverrides(),
            new Dictionary<string, string?>());

        RunConfigurationException exception = Assert.Throws<RunConfigurationException>(
            () => RunConfigurationValidator.Validate(configuration));

        Assert.Contains("reportSeconds", exception.Message);
        Assert.Contains("not supported", exception.Message);
    }

    [Fact]
    public void RetainWorkspacesUsesFlagEnvironmentUserConfigDefaultPrecedence()
    {
        var defaults = new UserConfiguration(RetainWorkspaces: true);

        RunConfiguration environmentResult = RunConfigurationResolver.Resolve(
            defaults,
            new RunConfigurationOverrides(),
            new Dictionary<string, string?> { ["WHARF_RETAIN_WORKSPACES"] = "0" });
        RunConfiguration flagResult = RunConfigurationResolver.Resolve(
            defaults,
            new RunConfigurationOverrides(RetainWorkspaces: true),
            new Dictionary<string, string?> { ["WHARF_RETAIN_WORKSPACES"] = "false" });

        Assert.False(environmentResult.RetainWorkspaces);
        Assert.True(flagResult.RetainWorkspaces);
    }

    [Fact]
    public void RejectsInvalidRetainWorkspacesEnvironmentValue()
    {
        RunConfigurationException exception = Assert.Throws<RunConfigurationException>(
            () => RunConfigurationResolver.Resolve(
                null,
                new RunConfigurationOverrides(),
                new Dictionary<string, string?>
                {
                    ["WHARF_RETAIN_WORKSPACES"] = "sometimes"
                }));

        Assert.Contains("retainWorkspaces", exception.Message);
    }

    [Fact]
    public void CapsPreparationConcurrencyAtTaskConcurrency()
    {
        RunConfiguration result = RunConfigurationResolver.Resolve(
            null,
            new RunConfigurationOverrides(N: 2, P: 8),
            new Dictionary<string, string?>());

        Assert.Equal(2, result.N);
        Assert.Equal(2, result.P);
    }

    [Fact]
    public void SupportsShortConcurrencyEnvironmentNames()
    {
        RunConfiguration result = RunConfigurationResolver.Resolve(
            null,
            new RunConfigurationOverrides(),
            new Dictionary<string, string?>
            {
                ["N"] = "1",
                ["P"] = "4"
            });

        Assert.Equal(1, result.N);
        Assert.Equal(1, result.P);
    }

    [Fact]
    public void ShortConcurrencyNamesOverrideWharfPrefixedValues()
    {
        RunConfiguration result = RunConfigurationResolver.Resolve(
            null,
            new RunConfigurationOverrides(),
            new Dictionary<string, string?>
            {
                ["N"] = "1",
                ["P"] = "1",
                ["WHARF_N"] = "4",
                ["WHARF_P"] = "4"
            });

        Assert.Equal(1, result.N);
        Assert.Equal(1, result.P);
    }

    [Fact]
    public void RendererIsDeterministicAndRedactsSecrets()
    {
        RunConfiguration configuration = RunConfigurationResolver.Resolve(
            new UserConfiguration(K: 2, OutputFormat: "json"),
            new RunConfigurationOverrides(OutputDirectory: "preview"),
            new Dictionary<string, string?> { ["WHARF_TOKEN"] = "top-secret" });

        RunConfigurationValidator.Validate(configuration);
        string first = RunConfigurationRenderer.Render(configuration, "json");
        string second = RunConfigurationRenderer.Render(configuration, "json");

        Assert.Equal(first, second);
        Assert.DoesNotContain("top-secret", first);
        Assert.Contains("[REDACTED]", first);
        Assert.NotNull(JsonDocument.Parse(first));
    }

    [Fact]
    public void SecretEnvironmentValuesAreRedacted()
    {
        RunConfiguration configuration = RunConfigurationResolver.Resolve(
            null,
            new RunConfigurationOverrides(),
            new Dictionary<string, string?> { ["WHARF_TOKEN"] = "environment-secret" });

        string output = RunConfigurationRenderer.Render(configuration, "json");

        Assert.DoesNotContain("environment-secret", output);
        Assert.Contains("[REDACTED]", output);
    }

    [Theory]
    [InlineData("""{"unknown":true}""")]
    [InlineData("""{"b":2}""")]
    [InlineData("""{"cliVersion":"1.2.3"}""")]
    [InlineData("""{"credentials":{"token":"synthetic"}}""")]
    [InlineData("""{"model":"custom"}""")]
    [InlineData("""{"outputDirectory":"elsewhere"}""")]
    [InlineData("""{"schemaVersion":"2.0"}""")]
    [InlineData("""{"k":0}""")]
    [InlineData("""{"p":0}""")]
    [InlineData("""{"retries":{"maxAttempts":-1}}""")]
    [InlineData("""{"timeouts":{"reportSeconds":30}}""")]
    [InlineData("""{"timeouts":{"agentSeconds":0}}""")]
    [InlineData("""{"k":1,"k":2}""")]
    [InlineData("""{"timeouts":{"agentSeconds":1,"agentSeconds":2}}""")]
    [InlineData("""{"timeouts":{"unknown":1}}""")]
    [InlineData("""{"K":2}""")]
    [InlineData("""{"k":"2"}""")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{")]
    public void InvalidUserConfigIsRejected(string json)
    {
        string home = Path.Combine(Path.GetTempPath(), $"wharf-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(home, ".wharf"));
        File.WriteAllText(Path.Combine(home, ".wharf", "config.json"), json);
        try
        {
            Assert.Throws<RunConfigurationException>(() => UserConfigurationLoader.Load(home));
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void MissingUserConfigDoesNotCreateDirectories()
    {
        string home = Path.Combine(Path.GetTempPath(), $"wharf-missing-{Guid.NewGuid():N}");
        Assert.Equal(new UserConfiguration(), UserConfigurationLoader.Load(home));
        Assert.False(Directory.Exists(home));
    }

    [Fact]
    public void InvalidOutputDirectoryIsRejected()
    {
        RunConfiguration configuration = RunConfigurationResolver.Resolve(
            null,
            new RunConfigurationOverrides(OutputDirectory: "/absolute"),
            new Dictionary<string, string?>());

        RunConfigurationException exception = Assert.Throws<RunConfigurationException>(
            () => RunConfigurationValidator.Validate(configuration));

        Assert.Contains("relative", exception.Message);
    }

    [Fact]
    public void InaccessibleUserConfigDoesNotSilentlyUseDefaults()
    {
        string home = Path.Combine(Path.GetTempPath(), $"wharf-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(home, ".wharf", "config.json"));
        try
        {
            RunConfigurationException exception = Assert.Throws<RunConfigurationException>(
                () => UserConfigurationLoader.Load(home));
            Assert.Contains("cannot read user configuration", exception.Message);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }
}
