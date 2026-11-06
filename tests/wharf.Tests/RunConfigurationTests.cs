using System.Text.Json;
using Wharf.Configuration;

namespace Wharf.Tests;

public sealed class RunConfigurationTests
{
    [Fact]
    public void PrecedenceIsFlagThenEnvironmentThenManifestThenDefault()
    {
        var manifest = new RunManifest(
            K: 3,
            N: 2,
            P: 1,
            Model: "manifest-model",
            CliVersion: "manifest-version");
        var environment = new Dictionary<string, string?>
        {
            ["WHARF_K"] = "5",
            ["WHARF_N"] = "4",
            ["WHARF_MODEL"] = "environment-model",
            ["WHARF_CLI_VERSION"] = "environment-version"
        };

        RunConfiguration result = RunConfigurationResolver.Resolve(
            manifest,
            new RunConfigurationOverrides(K: 7, CliVersion: "flag-version"),
            environment);

        Assert.Equal(7, result.K);
        Assert.Equal(4, result.N);
        Assert.Equal(1, result.P);
        Assert.Equal("environment-model", result.Model);
        Assert.Equal("default", result.Effort);
        Assert.Equal("flag-version", result.CliVersion);
    }

    [Fact]
    public void CliVersionUsesEnvironmentBeforeManifest()
    {
        RunConfiguration result = RunConfigurationResolver.Resolve(
            new RunManifest(CliVersion: "manifest-version"),
            new RunConfigurationOverrides(),
            new Dictionary<string, string?>
            {
                ["WHARF_CLI_VERSION"] = "environment-version"
            });

        Assert.Equal("environment-version", result.CliVersion);
    }

    [Fact]
    public void EffortUsesFlagBeforeEnvironmentAndManifest()
    {
        RunConfiguration result = RunConfigurationResolver.Resolve(
            new RunManifest(Model: "custom-model", Effort: "low"),
            new RunConfigurationOverrides(Effort: "high"),
            new Dictionary<string, string?>
            {
                ["WHARF_EFFORT"] = "medium"
            });

        Assert.Equal("high", result.Effort);
    }

    [Fact]
    public void EffortUsesEnvironmentBeforeManifest()
    {
        RunConfiguration result = RunConfigurationResolver.Resolve(
            new RunManifest(Model: "custom-model", Effort: "low"),
            new RunConfigurationOverrides(),
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
            new RunManifest(),
            new RunConfigurationOverrides(),
            new Dictionary<string, string?>());

        Assert.Equal(1, result.K);
        Assert.Equal("hydrafusion", result.Model);
        Assert.Equal("default", result.Effort);
        Assert.Equal("latest", result.CliVersion);
        Assert.Equal(1, result.Retries.MaxAttempts);
        Assert.False(result.RetainWorkspaces);
        Assert.False(result.ShortestFirst);
        Assert.Equal(8, result.ContainerCpus);
    }

    [Theory]
    [InlineData(2, "4", 6, 2)]
    [InlineData(null, "4", 6, 4)]
    [InlineData(null, null, 6, 6)]
    [InlineData(null, null, null, 8)]
    public void ContainerCpusUsesFlagEnvironmentManifestDefaultPrecedence(
        int? flag, string? environment, int? manifest, int expected)
    {
        RunConfiguration configuration = RunConfigurationResolver.Resolve(
            new RunManifest(ContainerCpus: manifest),
            new RunConfigurationOverrides(ContainerCpus: flag),
            new Dictionary<string, string?> { ["WHARF_CONTAINER_CPUS"] = environment });

        Assert.Equal(expected, configuration.ContainerCpus);
        RunConfigurationValidator.Validate(null, configuration);
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
            new RunManifest(ContainerCpus: limit),
            new RunConfigurationOverrides(),
            new Dictionary<string, string?>());

        RunConfigurationException exception = Assert.Throws<RunConfigurationException>(() =>
            RunConfigurationValidator.Validate(null, configuration));
        Assert.Contains("containerCpus", exception.Message);
    }

    [Fact]
    public void RendersCpuLimitInBothDryRunFormats()
    {
        string json = DryRunService.Execute(null, new RunConfigurationOverrides(ContainerCpus: 12),
            "json", new Dictionary<string, string?>());
        string text = DryRunService.Execute(null, new RunConfigurationOverrides(ContainerCpus: 12),
            "text", new Dictionary<string, string?>());
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.Equal(12, document.RootElement.GetProperty("containerCpus").GetInt32());
        Assert.Contains("containerCpus\t12", text);
    }

    [Fact]
    public void StrictManifestAcceptsContainerCpuLimit()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wharf-cpu-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{"schemaVersion":"1.0","containerCpus":12}""");
        try
        {
            Assert.Equal(12, RunManifestLoader.Load(path).ContainerCpus);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ShortestFirstIsOptIn()
    {
        RunConfiguration result = RunConfigurationResolver.Resolve(
            new RunManifest(),
            new RunConfigurationOverrides(ShortestFirst: true),
            new Dictionary<string, string?>());

        Assert.True(result.ShortestFirst);
    }

    [Fact]
    public void ReadsHydraFusionFromShortModelEnvironmentVariable()
    {
        RunConfiguration result = RunConfigurationResolver.Resolve(
            new RunManifest(),
            new RunConfigurationOverrides(),
            new Dictionary<string, string?> { ["MODEL"] = "hydrafusion" });

        Assert.Equal("hydrafusion", result.Model);
        Assert.Equal("default", result.Effort);
    }

    [Fact]
    public void RejectsInvalidHydraFusionEffort()
    {
        RunConfiguration result = RunConfigurationResolver.Resolve(
            new RunManifest(Model: "hydrafusion", Effort: "invalid"),
            new RunConfigurationOverrides(),
            new Dictionary<string, string?>());

        RunConfigurationException exception = Assert.Throws<RunConfigurationException>(
            () => RunConfigurationValidator.Validate(new RunManifest(), result));

        Assert.Contains("effort", exception.Message);
    }

    [Fact]
    public void AcceptsDefaultEffortFromEnvironment()
    {
        RunConfiguration result = RunConfigurationResolver.Resolve(
            new RunManifest(),
            new RunConfigurationOverrides(),
            new Dictionary<string, string?> { ["WHARF_EFFORT"] = "default" });

        RunConfigurationValidator.Validate(new RunManifest(), result);

        Assert.Equal("default", result.Effort);
    }

    [Fact]
    public void DefaultsConcurrencyToFour()
    {
        RunConfiguration result = RunConfigurationResolver.Resolve(
            new RunManifest(),
            new RunConfigurationOverrides(),
            new Dictionary<string, string?>());

        Assert.Equal(4, result.N);
        Assert.Equal(4, result.P);
    }

    [Fact]
    public void AllowsWholeTrialRetriesToBeDisabled()
    {
        RunConfiguration configuration = RunConfigurationResolver.Resolve(
            new RunManifest(Retries: new RetryPolicy(MaxAttempts: 0)),
            new RunConfigurationOverrides(),
            new Dictionary<string, string?>());

        RunConfigurationValidator.Validate(new RunManifest(), configuration);

        Assert.Equal(0, configuration.Retries.MaxAttempts);
    }

    [Fact]
    public void RejectsUnsupportedReportTimeout()
    {
        RunConfiguration configuration = RunConfigurationResolver.Resolve(
            new RunManifest(Timeouts: new PhaseTimeouts(ReportSeconds: 30)),
            new RunConfigurationOverrides(),
            new Dictionary<string, string?>());

        RunConfigurationException exception = Assert.Throws<RunConfigurationException>(
            () => RunConfigurationValidator.Validate(new RunManifest(), configuration));

        Assert.Contains("reportSeconds", exception.Message);
        Assert.Contains("not supported", exception.Message);
    }

    [Fact]
    public void RetainWorkspacesUsesFlagEnvironmentManifestDefaultPrecedence()
    {
        var manifest = new RunManifest(RetainWorkspaces: true);

        RunConfiguration environmentResult = RunConfigurationResolver.Resolve(
            manifest,
            new RunConfigurationOverrides(),
            new Dictionary<string, string?> { ["WHARF_RETAIN_WORKSPACES"] = "0" });
        RunConfiguration flagResult = RunConfigurationResolver.Resolve(
            manifest,
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
                new RunManifest(),
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
            new RunManifest(),
            new RunConfigurationOverrides(N: 2, P: 8),
            new Dictionary<string, string?>());

        Assert.Equal(2, result.N);
        Assert.Equal(2, result.P);
    }

    [Fact]
    public void SupportsShortConcurrencyEnvironmentNames()
    {
        RunConfiguration result = RunConfigurationResolver.Resolve(
            new RunManifest(),
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
            new RunManifest(),
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
            new RunManifest(
                K: 2,
                Output: new OutputSettings("json", "preview"),
                Credentials: new SecretSettings(Token: "top-secret")),
            new RunConfigurationOverrides(),
            new Dictionary<string, string?>());

        RunConfigurationValidator.Validate(new RunManifest(), configuration);
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
            new RunManifest(),
            new RunConfigurationOverrides(),
            new Dictionary<string, string?> { ["WHARF_TOKEN"] = "environment-secret" });

        string output = RunConfigurationRenderer.Render(configuration, "json");

        Assert.DoesNotContain("environment-secret", output);
        Assert.Contains("[REDACTED]", output);
    }

    [Fact]
    public void UnknownManifestKeyIsRejected()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wharf-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{"schemaVersion":"1.0","unknown":true}""");

        try
        {
            RunConfigurationException exception = Assert.Throws<RunConfigurationException>(
                () => RunManifestLoader.Load(path));
            Assert.Contains("invalid manifest", exception.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LegacyBuildConcurrencyManifestKeyIsRejected()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wharf-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{"schemaVersion":"1.0","b":2}""");

        try
        {
            RunConfigurationException exception = Assert.Throws<RunConfigurationException>(
                () => RunManifestLoader.Load(path));
            Assert.Contains("invalid manifest", exception.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void InvalidOutputDirectoryIsRejected()
    {
        RunConfiguration configuration = RunConfigurationResolver.Resolve(
            new RunManifest(Output: new OutputSettings(Directory: "/absolute")),
            new RunConfigurationOverrides(),
            new Dictionary<string, string?>());

        RunConfigurationException exception = Assert.Throws<RunConfigurationException>(
            () => RunConfigurationValidator.Validate(new RunManifest(), configuration));

        Assert.Contains("relative", exception.Message);
    }

    [Fact]
    public void ManifestMustDeclareSchemaVersion()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wharf-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{"k":2}""");

        try
        {
            RunConfigurationException exception = Assert.Throws<RunConfigurationException>(
                () => RunManifestLoader.Load(path));
            Assert.Contains("schemaVersion", exception.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
