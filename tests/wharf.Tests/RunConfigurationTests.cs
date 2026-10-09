using System.Text.Json;
using Wharf.Configuration;

namespace Wharf.Tests;

public sealed class RunConfigurationTests
{
    [Fact]
    public void PrecedenceIsFlagThenEnvironmentThenManifestThenDefault()
    {
        var manifest = new RunManifest(K: 3, N: 2, P: 1, Model: "manifest-model");
        var environment = new Dictionary<string, string?>
        {
            ["WHARF_K"] = "5",
            ["WHARF_N"] = "4",
            ["WHARF_MODEL"] = "environment-model"
        };

        RunConfiguration result = RunConfigurationResolver.Resolve(
            manifest,
            new RunConfigurationOverrides(K: 7),
            environment);

        Assert.Equal(7, result.K);
        Assert.Equal(4, result.N);
        Assert.Equal(1, result.P);
        Assert.Equal("environment-model", result.Model);
        Assert.Equal("max", result.Effort);
    }

    [Fact]
    public void UsesGptFiveSixLunaWhenModelIsNotConfigured()
    {
        RunConfiguration result = RunConfigurationResolver.Resolve(
            new RunManifest(),
            new RunConfigurationOverrides(),
            new Dictionary<string, string?>());

        Assert.Equal(1, result.K);
        Assert.Equal("gpt-5.6-luna", result.Model);
        Assert.Equal("max", result.Effort);
        Assert.Equal("latest", result.CliVersion);
        Assert.Equal(1, result.Retries.MaxAttempts);
        Assert.False(result.RetainWorkspaces);
        Assert.False(result.ShortestFirst);
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
