using Wharf.Run;

namespace Wharf.Tests;

public sealed class RunServiceTests
{
    [Fact]
    public void GivenEffectiveVersion_WhenBuildingJobDirectoryName_UsesEffectiveVersion()
    {
        string result = RunService.BuildJobDirectoryName(
            "hydrafusion",
            "latest",
            "1.0.91");

        Assert.Equal("copilot-hydrafusion-max-1.0.91", result);
    }

    [Fact]
    public void GivenSetupFailure_WhenBuildingJobDirectoryName_UsesRequestedVersion()
    {
        string result = RunService.BuildJobDirectoryName(
            "hydrafusion",
            "latest",
            null);

        Assert.Equal("copilot-hydrafusion-max-latest", result);
    }
}
