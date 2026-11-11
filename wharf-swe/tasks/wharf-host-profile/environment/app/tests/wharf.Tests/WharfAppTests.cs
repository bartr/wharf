using Wharf;

namespace Wharf.Tests;

public sealed class WharfAppTests
{
    [Fact]
    public async Task VersionCommandSucceeds()
    {
        int exitCode = await WharfApp.RunAsync(["version"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task OperationalCommandFailsExplicitly()
    {
        int exitCode = await WharfApp.RunAsync(["run"]);

        Assert.Equal(1, exitCode);
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
