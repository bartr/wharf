using System.Diagnostics;
using System.Text.RegularExpressions;
using Wharf.Tasks;

namespace Wharf.Image;

public interface IAgentImagePreparer
{
    Task<string> PrepareAsync(
        HarborTask task,
        string evidenceDirectory,
        CancellationToken cancellationToken = default);
}

public sealed class DockerAgentImagePreparer : IAgentImagePreparer
{
    private readonly ImagePullCoordinator imagePulls;

    public DockerAgentImagePreparer(ImagePullCoordinator? imagePulls = null) =>
        this.imagePulls = imagePulls ?? new ImagePullCoordinator(
            new DockerImageClient(),
            new SystemAsyncDelay(),
            new SystemRandomSource(),
            new NullImagePullTelemetry(),
            new ImagePullConsole(0));

    public async Task<string> PrepareAsync(
        HarborTask task,
        string evidenceDirectory,
        CancellationToken cancellationToken = default)
    {
        string context = Path.Combine(task.RootPath, "environment");
        string dockerfile = Path.Combine(context, "Dockerfile");
        if (!File.Exists(dockerfile))
            throw new AgentImageException($"agent Dockerfile does not exist: {dockerfile}");

        string baseImage = ReadBaseImage(dockerfile);
        string image = $"wharf-agent:{task.SourceChecksum[..12]}";
        Directory.CreateDirectory(evidenceDirectory);
        await imagePulls.PrepareAsync(baseImage, cancellationToken);

        ProcessResult build = await RunDockerAsync(
            ["image", "build", "--file", dockerfile, "--tag", image, context],
            cancellationToken);
        File.WriteAllText(
            Path.Combine(evidenceDirectory, "image-build.log"),
            build.StandardOutput + build.StandardError);
        return image;
    }

    private static string ReadBaseImage(string dockerfile)
    {
        foreach (string line in File.ReadLines(dockerfile))
        {
            Match match = Regex.Match(line, @"^\s*FROM\s+([^\s]+)", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value;
        }

        throw new AgentImageException($"Dockerfile has no FROM instruction: {dockerfile}");
    }

    private static async Task<ProcessResult> RunDockerAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
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
            ?? throw new AgentImageException("Docker process could not be started");
        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var result = new ProcessResult(process.ExitCode, await output, await error);
        if (result.ExitCode != 0)
            throw new AgentImageException($"Docker command failed: {SelectError(result)}");
        return result;
    }

    private static string SelectError(ProcessResult result) =>
        string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}

public sealed class AgentImageException(string message) : Exception(message);
