using Wharf.Run;
using Wharf.Configuration;
using Wharf.Copilot;
using Wharf.Recovery;
using Wharf.Tasks;

namespace Wharf.Tests;

public sealed class RunServiceTests
{
    [Theory]
    [InlineData(8, 1, 1, 4, "all", null)]
    [InlineData(4, 1, 1, 4, "all", null)]
    [InlineData(8, 1, 4, 4, "all", 8)]
    [InlineData(4, 1, 4, 4, "all", 4)]
    [InlineData(8, 1, 4, 1, "all", null)]
    [InlineData(8, 2, 1, 1, "all", null)]
    [InlineData(4, 2, 4, 1, "all", null)]
    [InlineData(8, 1, 1, 1, "smoke", null)]
    [InlineData(4, 2, 4, 4, "smoke", null)]
    [InlineData(8, 2, 1, 4, "all", 8)]
    [InlineData(4, 2, 1, 4, "all", 4)]
    [InlineData(null, 2, 4, 4, "all", null)]
    public void CpuLimitsFollowTrialConcurrencyRatherThanDistinctTaskCount(
        int? configuredLimit, int taskCount, int k, int n, string attemptPrefix, int? expected)
    {
        Assert.Equal(expected, RunService.ResolveContainerCpuLimit(
            configuredLimit, taskCount, k, n, attemptPrefix));
    }

    [Theory]
    [InlineData(1, 1, 4, null)]
    [InlineData(1, 4, 4, 8)]
    [InlineData(2, 1, 4, 8)]
    [InlineData(2, 4, 1, null)]
    public void ResumeUsesOriginalTaskSelectionRatherThanRemainingTaskCount(
        int selectedTaskCount, int k, int n, int? expected)
    {
        string taskRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "../../../../fixtures/harbor"));
        string runDirectory = Path.Combine(Path.GetTempPath(), $"wharf-cpu-policy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(runDirectory);
        try
        {
            HarborTask[] selectedTasks = new HarborTaskLoader().Load(taskRoot)
                .Take(selectedTaskCount).ToArray();
            Assert.Equal(selectedTaskCount, selectedTasks.Length);
            RunConfiguration configuration = RunConfigurationResolver.Resolve(
                null, new RunConfigurationOverrides(K: k, N: n), new Dictionary<string, string?>());
            RetainedRunMetadataStore.Write(runDirectory, taskRoot, configuration, selectedTasks);

            Assert.Equal(expected, RunService.ResolveContainerCpuLimit(
                configuration.ContainerCpus, 1, k, n, "all", runDirectory));
        }
        finally
        {
            Directory.Delete(runDirectory, recursive: true);
        }
    }

    [Fact]
    public void GivenHydraFusion_WhenBuildingJobDirectoryName_UsesDefaultEffort()
    {
        string result = RunService.BuildJobDirectoryName(
            "hydrafusion",
            "max",
            "latest",
            "1.0.91");

        Assert.Equal("copilot-hydrafusion-default-1.0.91", result);
    }

    [Fact]
    public void GivenSetupFailure_WhenBuildingJobDirectoryName_UsesRequestedVersion()
    {
        string result = RunService.BuildJobDirectoryName(
            "hydrafusion",
            "max",
            "latest",
            null);

        Assert.Equal("copilot-hydrafusion-default-latest", result);
    }

    [Fact]
    public void GivenConfiguredEffort_WhenBuildingJobDirectoryName_UsesConfiguredEffort()
    {
        string result = RunService.BuildJobDirectoryName(
            "mai-code-1.1-flash",
            "medium",
            "latest",
            "1.0.95");

        Assert.Equal("copilot-mai-code-1.1-flash-medium-1.0.95", result);
    }

    [Fact]
    public void ConfiguredTokenReplacesLocalCredentialFile()
    {
        (CopilotCredentialSource source, string? value) =
            RunService.ResolveCredential(new SecretSettings(
                ApiKey: "api-key",
                Token: "token",
                Password: "password"));

        Assert.Equal(CopilotCredentialSourceKind.Environment, source.Kind);
        Assert.Equal("GH_TOKEN", source.Reference);
        Assert.Equal("token", value);
    }

    [Fact]
    public void MissingConfiguredCredentialUsesLocalCredentialFile()
    {
        (CopilotCredentialSource source, string? value) =
            RunService.ResolveCredential(new SecretSettings());

        Assert.Equal(CopilotCredentialSourceKind.File, source.Kind);
        Assert.Equal("~/.wharf/copilot", source.Reference);
        Assert.Null(value);
    }

    [Fact]
    public void ConfiguredTimeoutOverridesTaskAndDefaultValues()
    {
        Assert.Equal(60, RunService.ResolveTimeoutSeconds(60, 120, 180));
        Assert.Equal(120, RunService.ResolveTimeoutSeconds(null, 120, 180));
        Assert.Equal(180, RunService.ResolveTimeoutSeconds(null, null, 180));
    }
}
