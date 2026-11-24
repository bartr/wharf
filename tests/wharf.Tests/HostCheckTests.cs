using System.Runtime.InteropServices;
using Wharf.Host;

namespace Wharf.Tests;

public sealed class HostCheckTests
{
    [Theory]
    [InlineData("24.04")]
    [InlineData("24.10")]
    [InlineData("25.10")]
    [InlineData("26.04")]
    [InlineData("30.04")]
    public void UbuntuAtOrAboveMinimumVersionIsSupported(string version)
    {
        var probe = new PlatformProbe(
            () => $"ID=ubuntu\nVERSION_ID=\"{version}\"\n", Architecture.X64);

        HostCheck result = probe.Check(new HostCheckOptions());

        Assert.True(result.Passed);
        Assert.Contains("ubuntu24.04+=True", result.Detail);
    }

    [Theory]
    [InlineData("ID=\"ubuntu\"\nVERSION_ID=26.04")]
    [InlineData("ID='ubuntu'\nVERSION_ID='24.04'")]
    [InlineData("NAME=\"Ubuntu\"\r\nID=ubuntu\r\nVERSION_ID=\"26.04\"\r\n")]
    public void OsReleaseSupportsQuotedAndUnquotedValues(string release)
    {
        var probe = new PlatformProbe(() => release, Architecture.X64);

        Assert.True(probe.Check(new HostCheckOptions()).Passed);
    }

    [Theory]
    [InlineData("ID=ubuntu\nVERSION_ID=\"22.04\"")]
    [InlineData("ID=ubuntu\nVERSION_ID=\"24.03\"")]
    [InlineData("ID=ubuntu\nVERSION_ID=\"9.10\"")]
    [InlineData("ID=debian\nVERSION_ID=\"26.04\"\nID_LIKE=ubuntu")]
    [InlineData("ID=ubuntu-derivative\nVERSION_ID=\"26.04\"")]
    [InlineData("# ID=ubuntu\nID=debian\nVERSION_ID=\"26.04\"")]
    [InlineData("ID=ubuntu\nVERSION_ID=\"invalid\"")]
    [InlineData("ID=ubuntu\nVERSION_ID=\"\"")]
    [InlineData("ID=ubuntu")]
    [InlineData("VERSION_ID=\"26.04\"")]
    [InlineData("")]
    public void UnsupportedOrUnidentifiedReleaseIsRejected(string release)
    {
        var probe = new PlatformProbe(() => release, Architecture.X64);

        HostCheckReport report = new HostCheckService([probe]).Check(new HostCheckOptions());

        Assert.False(report.IsSuccess);
        Assert.True(report.HasUnsupportedHost);
        Assert.Equal(3, report.ExitCode);
    }

    [Theory]
    [InlineData(Architecture.Arm64)]
    [InlineData(Architecture.X86)]
    public void UbuntuStillRequiresAmd64(Architecture architecture)
    {
        var probe = new PlatformProbe(
            () => "ID=ubuntu\nVERSION_ID=\"26.04\"", architecture);

        Assert.False(probe.Check(new HostCheckOptions()).Passed);
    }

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
        Assert.True(report.HasUnsupportedHost);
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
    public void DefaultCapacityThresholdsMatchSupportedMinimums()
    {
        HostCheckOptions options = new();

        Assert.Equal(4L * 1024 * 1024 * 1024, options.MinimumMemoryBytes);
        Assert.Equal(2, options.MinimumCpuCount);
        Assert.Equal(1_024, options.MinimumInotifyInstances);
        Assert.Equal(65_536, options.MinimumInotifyWatches);
    }

    [Fact]
    public void CapacityChecksDockerFilesystemAndSharesDaemonInspection()
    {
        int inspections = 0;
        var docker = new DockerProbe(() =>
        {
            inspections++;
            return """
                {"DockerRootDir":"/mnt/data/docker","MemoryLimit":true,"PidsLimit":true,"Plugins":{"Network":["bridge"]}}
                """;
        });
        var capacity = new CapacityProbe(docker, path =>
            path == "/mnt/data/docker" ? 64L * 1024 * 1024 * 1024 : 8L * 1024 * 1024 * 1024);

        HostCheckReport report = new HostCheckService([docker, capacity]).Check(DiskOnlyOptions());

        Assert.True(report.IsSuccess);
        Assert.Equal(1, inspections);
        Assert.Contains("diskPath=/mnt/data/docker", report.Checks.Single(check => check.Name == "capacity").Detail);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, false)]
    public void DockerFilesystemMustMeetDiskThreshold(long difference, bool passed)
    {
        HostCheckOptions options = DiskOnlyOptions() with { AllowUnsupportedHost = true };
        var docker = new DockerProbe(() => """{"DockerRootDir":"/mnt/data/docker"}""");
        var capacity = new CapacityProbe(docker, _ => options.MinimumDiskBytes + difference);

        HostCheckReport report = new HostCheckService([capacity]).Check(options);

        Assert.Equal(passed, report.IsSuccess);
        Assert.Equal(!passed, report.HasBlockingFailure);
        Assert.Contains("diskPath=/mnt/data/docker", report.Checks.Single().Detail);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"DockerRootDir":null}""")]
    [InlineData("""{"DockerRootDir":""}""")]
    [InlineData("""{"DockerRootDir":"relative/docker"}""")]
    [InlineData("""{"DockerRootDir":42}""")]
    [InlineData("invalid-json")]
    public void InvalidDockerStorageDiscoveryBlocksWithoutRootFallback(string info)
    {
        var docker = new DockerProbe(() => info);
        var capacity = new CapacityProbe(docker, _ =>
            throw new Xunit.Sdk.XunitException("Invalid Docker storage must not fall back to another disk"));

        HostCheckReport report = new HostCheckService([capacity]).Check(
            DiskOnlyOptions() with { AllowUnsupportedHost = true });

        Assert.True(report.HasBlockingFailure);
        Assert.Equal(4, report.ExitCode);
        Assert.Contains("capacity probe failed:", report.Checks.Single().Detail);
    }

    [Fact]
    public void DockerDiscoveryFailureBlocksBothChecksWithoutRepeatingInspection()
    {
        int inspections = 0;
        var docker = new DockerProbe(() =>
        {
            inspections++;
            throw new InvalidOperationException("Docker daemon unavailable");
        });
        var capacity = new CapacityProbe(docker);

        HostCheckReport report = new HostCheckService([docker, capacity]).Check(DiskOnlyOptions());

        Assert.Equal(1, inspections);
        Assert.True(report.HasBlockingFailure);
        Assert.All(report.Checks, check =>
        {
            Assert.False(check.Passed);
            Assert.Contains("Docker daemon unavailable", check.Detail);
        });
    }

    [Fact]
    public void InaccessibleDockerFilesystemIsBlocking()
    {
        var docker = new DockerProbe(() => """{"DockerRootDir":"/mnt/data/docker"}""");
        var capacity = new CapacityProbe(docker, _ =>
            throw new UnauthorizedAccessException("Docker storage is inaccessible"));

        HostCheck result = capacity.Check(DiskOnlyOptions());

        Assert.False(result.Passed);
        Assert.False(result.Overridable);
        Assert.Contains("Docker storage is inaccessible", result.Detail);
    }

    private static HostCheckOptions DiskOnlyOptions() => new(
        MinimumMemoryBytes: 0,
        MinimumCpuCount: 0,
        MinimumNofileSoft: 0,
        MinimumNofileHard: 0,
        MinimumInotifyInstances: 0,
        MinimumInotifyWatches: 0,
        MinimumPids: 0);

    private sealed class FixedProbe(HostCheck result) : IHostProbe
    {
        public HostCheck Check(HostCheckOptions options) => result;
    }
}
