using Wharf.Host;

namespace Wharf.Tests;

public sealed class HostCheckTests
{
    [Fact]
    public void UnsupportedPlatformCanBeOverriddenWithoutOverridingDockerFailure()
    {
        var service = new HostCheckService(
        [
            new FixedProbe(new HostCheck("platform", false, true, "unsupported")),
            new FixedProbe(new HostCheck("docker", false, false, "daemon unavailable"))
        ]);

        HostCheckReport report = service.Check(new HostCheckOptions(AllowUnsupportedHost: true));

        Assert.True(report.Checks.Single(check => check.Name == "platform").Passed);
        Assert.False(report.Checks.Single(check => check.Name == "docker").Passed);
        Assert.True(report.HasBlockingFailure);
        Assert.Equal(4, report.ExitCode);
    }

    [Fact]
    public void PassingProbesProduceSuccess()
    {
        var service = new HostCheckService(
        [
            new FixedProbe(new HostCheck("platform", true, true, "supported")),
            new FixedProbe(new HostCheck("docker", true, false, "reachable")),
            new FixedProbe(new HostCheck("capacity", true, false, "sufficient"))
        ]);

        HostCheckReport report = service.Check(new HostCheckOptions());

        Assert.True(report.IsSuccess);
        Assert.Equal(0, report.ExitCode);
    }

    [Fact]
    public void DefaultInotifyThresholdsMatchLinuxKernelSettings()
    {
        HostCheckOptions options = new();

        Assert.Equal(1_024, options.MinimumInotifyInstances);
        Assert.Equal(65_536, options.MinimumInotifyWatches);
    }

    private sealed class FixedProbe(HostCheck result) : IHostProbe
    {
        public HostCheck Check(HostCheckOptions options) => result;
    }
}
