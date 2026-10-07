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
    public async Task RunCommandExecutesTaskDiscovery()
    {
        string relativeOutput = "run-test";
        string outputDirectory = Path.GetFullPath(relativeOutput);
        try
        {
            int exitCode = await WharfApp.RunAsync([
                "run",
                "--tasks", Path.Combine(FindRepositoryRoot(), "smoke-tasks"),
                "--output-directory", relativeOutput
            ]);

            Assert.Equal(0, exitCode);
        }

        finally
        {
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !Directory.Exists(Path.Combine(directory.FullName, "smoke-tasks")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("repository root not found");
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
