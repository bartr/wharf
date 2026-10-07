using Wharf.Host;
using Wharf.Logging;
using Wharf.Smoke;

namespace Wharf.Tests;

public sealed class SmokeServiceTests
{
    [Fact]
    public void SelectsOneTaskAndCallsRunExecutor()
    {
        string directory = Path.Combine(Path.GetTempPath(), "wharf-smoke-" + Guid.NewGuid().ToString("N"));
        try
        {
            HostCheckReport host = new([
                new HostCheck("platform", true, true, "supported"),
                new HostCheck("docker", true, false, "reachable"),
                new HostCheck("capacity", true, false, "sufficient")
            ]);
            RecordingExecutor executor = new();
            using WharfLogger logger = WharfLogger.Create(new());
            SmokeResult result = new SmokeService(runExecutor: executor).Run(
                Path.Combine(FindRepositoryRoot(), "smoke-tasks"),
                directory,
                host,
                logger);

            Assert.Equal("run-no-op", result.Status);
            Assert.Equal(0, result.ExitCode);
            Assert.Equal("smoke-go", result.TaskId);
            Assert.Equal("smoke-go", executor.TaskId);
            Assert.True(File.Exists(FindSmokeFile(directory, "smoke-selection.json")));
            Assert.True(Directory.Exists(FindSmokeDirectory(directory, "artifacts")));
            Assert.True(Directory.Exists(FindSmokeDirectory(directory, "agent")));
            Assert.True(Directory.Exists(FindSmokeDirectory(directory, "verifier")));
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
            using WharfLogger logger = WharfLogger.Create(new());
            SmokeResult result = new SmokeService().Run(
                Path.Combine(FindRepositoryRoot(), "smoke-tasks"),
                directory,
                host,
                logger);

            Assert.Equal("qualification-blocked", result.Status);
            Assert.Equal(4, result.ExitCode);
            Assert.True(File.Exists(FindSmokeFile(directory, "smoke-selection.json")));
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

    private static string FindSmokeFile(string directory, string fileName) =>
        Directory.GetFiles(directory, fileName, SearchOption.AllDirectories).Single();

    private static string FindSmokeDirectory(string directory, string name) =>
        Directory.GetDirectories(directory, name, SearchOption.AllDirectories).Single();

    private sealed class RecordingExecutor : ISmokeRunExecutor
    {
        public string? TaskId { get; private set; }

        public SmokeRunResult Run(SmokeRunRequest request, WharfLogger logger)
        {
            TaskId = request.Task.TaskId;
            return new SmokeRunResult("run-no-op", request.Task.TaskId, request.TaskRoot, request.OutputDirectory);
        }
    }
}
