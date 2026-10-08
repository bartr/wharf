using Wharf.Host;
using Wharf.Logging;
using Wharf.Smoke;

namespace Wharf.Tests;

public sealed class SmokeServiceTests
{
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
            using WharfLogger logger = WharfLogger.Create(new());
            SmokeResult result = new SmokeService().Run(
                Path.Combine(FindRepositoryRoot(), "smoke-tasks"),
                directory,
                host,
                logger);

            Assert.Equal("qualification-blocked", result.Status);
            Assert.Equal(4, result.ExitCode);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RejectsMissingSmokeTaskDirectory()
    {
        using WharfLogger logger = WharfLogger.Create(new());
        HostCheckReport host = new([]);

        Assert.Throws<SmokeException>(() => new SmokeService().Run(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            Path.GetTempPath(),
            host,
            logger));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "smoke-tasks")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("repository root not found");
    }
}
