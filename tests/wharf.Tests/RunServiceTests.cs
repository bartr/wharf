using Wharf.Run;

namespace Wharf.Tests;

public sealed class RunServiceTests
{
    [Fact]
    public void GivenEffectiveVersion_WhenBuildingJobDirectoryName_UsesEffectiveVersion()
    {
        string result = RunService.BuildJobDirectoryName(
            "hydrafusion",
            "max",
            "latest",
            "1.0.91");

        Assert.Equal("copilot-hydrafusion-max-1.0.91", result);
    }

    [Fact]
    public void GivenSetupFailure_WhenBuildingJobDirectoryName_UsesRequestedVersion()
    {
        string result = RunService.BuildJobDirectoryName(
            "hydrafusion",
            "max",
            "latest",
            null);

        Assert.Equal("copilot-hydrafusion-max-latest", result);
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
}
