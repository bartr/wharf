using Wharf.Run;
using Wharf.Configuration;
using Wharf.Copilot;

namespace Wharf.Tests;

public sealed class RunServiceTests
{
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
        Assert.Equal("~/.secrets/copilot_a", source.Reference);
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
