using Wharf.Host;
using Wharf.Smoke;

namespace Wharf.Tests;

public sealed class SmokeServiceTests
{
    [Fact]
    public void RunsLocalFixtureAndWritesArtifacts()
    {
        string directory = Path.Combine(Path.GetTempPath(), "wharf-smoke-" + Guid.NewGuid().ToString("N"));
        try
        {
            HostCheckReport host = new([
                new HostCheck("platform", true, true, "supported"),
                new HostCheck("docker", true, false, "reachable"),
                new HostCheck("capacity", true, false, "sufficient")
            ]);
            SmokeResult result = new SmokeService().Run(directory, host);

            Assert.Equal("completed", result.Status);
            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(result.TrialPath));
            Assert.True(File.Exists(result.ReportPath));
            Assert.Contains("verified", File.ReadAllText(Path.Combine(directory, "smoke.txt")));
            Assert.True(File.Exists(Path.Combine(directory, "report.jsonl")));
            Assert.True(File.Exists(Path.Combine(directory, "host-check.json")));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void PreservesHostQualificationFailureInResult()
    {
        string directory = Path.Combine(Path.GetTempPath(), "wharf-smoke-" + Guid.NewGuid().ToString("N"));
        try
        {
            HostCheckReport host = new([
                new HostCheck("platform", true, true, "supported"),
                new HostCheck("docker", false, false, "daemon unavailable")
            ]);
            SmokeResult result = new SmokeService().Run(directory, host);

            Assert.Equal("qualification-blocked", result.Status);
            Assert.Equal(4, result.ExitCode);
            Assert.True(File.Exists(result.ReportPath));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
