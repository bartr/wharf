using System.Diagnostics;
using System.Text.Json;
using Wharf.Docker;
using Wharf.Image;
using Wharf.Tasks;

namespace Wharf.Tests;

public sealed class DockerAgentImagePreparerTests
{
    private const string TaskId = "aiomonitor-task-snapshots-diff";

    [Fact]
    public async Task EnvironmentImageTaskBuildsAndRemovesUniqueTrialImage()
    {
        string repositoryRoot = FindRepositoryRoot();
        HarborTask task = new HarborTaskLoader()
            .Load(Path.Combine(repositoryRoot, "tasks"))
            .Single(task => task.TaskId == TaskId);
        string image = $"agent-{TaskId}__image-smoke-{Guid.NewGuid():N}:latest";
        string evidenceDirectory = Path.Combine(
            Path.GetTempPath(),
            $"wharf-{TaskId}-{Guid.NewGuid():N}");
        var preparer = new DockerAgentImagePreparer();

        string environmentImage = Assert.IsType<string>(task.EnvironmentImage);
        Assert.Equal(
            "public.ecr.aws/d3j8x8q7/swe-bench-202605:" +
            "kh75rc2q0zhmsqwk7wewfwwtrx830v2n-v1.1",
            environmentImage);

        try
        {
            string prepared = await preparer.PrepareAsync(
                task,
                evidenceDirectory,
                image);

            Assert.Equal(image, prepared);
            Assert.Contains(
                $"FROM {environmentImage}",
                File.ReadAllText(Path.Combine(
                    evidenceDirectory,
                    "Dockerfile.wharf")),
                StringComparison.Ordinal);

            JsonElement parent = await InspectImageAsync(environmentImage);
            JsonElement child = await InspectImageAsync(image);
            Assert.NotEqual(
                parent.GetProperty("Id").GetString(),
                child.GetProperty("Id").GetString());
            Assert.Equal(
                image,
                child.GetProperty("Config")
                    .GetProperty("Labels")
                    .GetProperty("com.bartr.wharf.trial-image")
                    .GetString());

            await preparer.RemoveImageAsync(image);

            Assert.False(await ImageExistsAsync(image));
            Assert.True(await ImageExistsAsync(environmentImage));
        }
        finally
        {
            await RunDockerAsync(["image", "rm", "--force", image], allowFailure: true);
            DockerRunCleanup.ForgetImage(image);
            if (Directory.Exists(evidenceDirectory))
                Directory.Delete(evidenceDirectory, recursive: true);
        }
    }

    private static async Task<JsonElement> InspectImageAsync(string image)
    {
        ProcessResult result = await RunDockerAsync(["image", "inspect", image]);
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        return document.RootElement[0].Clone();
    }

    private static async Task<bool> ImageExistsAsync(string image) =>
        (await RunDockerAsync(
            ["image", "inspect", image],
            allowFailure: true)).ExitCode == 0;

    private static async Task<ProcessResult> RunDockerAsync(
        IReadOnlyList<string> arguments,
        bool allowFailure = false)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Docker process could not be started");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var result = new ProcessResult(process.ExitCode, await output, await error);
        if (!allowFailure && result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(result.StandardError)
                    ? result.StandardOutput.Trim()
                    : result.StandardError.Trim());
        }

        return result;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !Directory.Exists(Path.Combine(
                   directory.FullName,
                   "tasks",
                   TaskId)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("repository root not found");
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
