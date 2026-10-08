using System.Diagnostics;
using System.Text.RegularExpressions;
using Wharf.Tasks;

namespace Wharf.Image;

public interface IAgentImagePreparer
{
    Task<string> PrepareAsync(
        HarborTask task,
        string evidenceDirectory,
        string imageName,
        Action<string>? progress = null,
        CancellationToken cancellationToken = default);

    Task RemoveImageAsync(
        string image,
        CancellationToken cancellationToken = default);
}

public sealed class DockerAgentImagePreparer : IAgentImagePreparer
{
    private readonly ImagePullCoordinator imagePulls;
    private readonly Action<string>? progress;

    public DockerAgentImagePreparer(
        ImagePullCoordinator? imagePulls = null,
        Action<string>? progress = null)
    {
        this.imagePulls = imagePulls ?? new ImagePullCoordinator(
            new DockerImageClient(),
            new SystemAsyncDelay(),
            new SystemRandomSource(),
            new NullImagePullTelemetry(),
            new ImagePullConsole(0));
        this.progress = progress;
    }

    public async Task<string> PrepareAsync(
        HarborTask task,
        string evidenceDirectory,
        string imageName,
        Action<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress ??= this.progress;
        Directory.CreateDirectory(evidenceDirectory);
        if (!string.IsNullOrWhiteSpace(task.EnvironmentImage))
        {
            progress?.Invoke($"agent image checking {task.EnvironmentImage}");
            ImagePullResult environmentImage = await imagePulls.PrepareAsync(
                task.EnvironmentImage,
                cancellationToken);
            progress?.Invoke(
                $"agent image {environmentImage.Outcome}: {task.EnvironmentImage}");
            File.WriteAllText(
                Path.Combine(evidenceDirectory, "image-pull.json"),
                System.Text.Json.JsonSerializer.Serialize(environmentImage));
            return task.EnvironmentImage;
        }

        string context = Path.Combine(task.RootPath, "environment");
        string dockerfile = Path.Combine(context, "Dockerfile");
        if (!File.Exists(dockerfile))
            throw new AgentImageException($"agent Dockerfile does not exist: {dockerfile}");

        string configuredBaseImage = ReadBaseImage(dockerfile);
        string baseImage = ResolveLocalBaseImage(configuredBaseImage);
        if (string.IsNullOrWhiteSpace(imageName))
            throw new AgentImageException("agent image name is required");
        string image = imageName;
        progress?.Invoke($"agent image preparing task {task.TaskId}");
        if (!string.Equals(configuredBaseImage, baseImage, StringComparison.Ordinal))
            progress?.Invoke($"agent image substituting {configuredBaseImage} with {baseImage}");
        progress?.Invoke($"agent image checking base {baseImage}");
        ImagePullResult baseResult = await imagePulls.PrepareAsync(baseImage, cancellationToken);
        progress?.Invoke($"agent image base {baseResult.Outcome}: {baseImage}");
        progress?.Invoke($"agent image building {image}");

        string buildDockerfile = Path.Combine(evidenceDirectory, "Dockerfile.wharf");
        File.WriteAllLines(
            buildDockerfile,
            RewriteBaseImage(File.ReadLines(dockerfile), configuredBaseImage, baseImage));
        ProcessResult build = await RunDockerAsync(
            ["image", "build", "--file", buildDockerfile, "--tag", image, context],
            cancellationToken);
        File.WriteAllText(
            Path.Combine(evidenceDirectory, "image-build.log"),
            build.StandardOutput + build.StandardError);
        progress?.Invoke($"agent image built {image}");
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

    private static IEnumerable<string> RewriteBaseImage(
        IEnumerable<string> lines,
        string configuredBaseImage,
        string baseImage)
    {
        bool replaced = false;
        foreach (string line in lines)
        {
            if (!replaced &&
                Regex.IsMatch(line, @"^\s*FROM\s+" + Regex.Escape(configuredBaseImage) + @"(?:\s|$)", RegexOptions.IgnoreCase))
            {
                yield return Regex.Replace(
                    line,
                    Regex.Escape(configuredBaseImage),
                    baseImage,
                    RegexOptions.IgnoreCase);
                replaced = true;
            }
            else
            {
                yield return line;
            }
        }
    }

    private static string ResolveLocalBaseImage(string image) =>
        image.Equals(
            "public.ecr.aws/x8v8d7g8/mars-base:latest",
            StringComparison.OrdinalIgnoreCase)
            ? "mars-base:latest"
            : image;

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
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }
        var result = new ProcessResult(process.ExitCode, await output, await error);
        if (result.ExitCode != 0)
            throw new AgentImageException($"Docker command failed: {SelectError(result)}");
        return result;
    }

    private static string SelectError(ProcessResult result) =>
        string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();

    public async Task RemoveImageAsync(
        string image,
        CancellationToken cancellationToken = default)
    {
        ProcessResult result = await RunDockerAsync(
            ["image", "rm", image],
            cancellationToken);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}

public sealed class AgentImageException(string message) : Exception(message);
