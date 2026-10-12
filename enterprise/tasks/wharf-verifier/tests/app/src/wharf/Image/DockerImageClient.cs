using System.Diagnostics;

namespace Wharf.Image;

public sealed class DockerImageClient : IImageClient
{
    public async Task<bool> ExistsAsync(string image, CancellationToken cancellationToken)
    {
        ProcessResult result = await RunAsync(
            ["image", "inspect", image],
            cancellationToken);
        if (result.ExitCode == 0)
        {
            return true;
        }

        if (result.StandardError.Contains("No such image", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new DockerImageException(
            $"Docker image inspection failed for {image}: {SelectError(result)}");
    }

    public async Task<ImagePullAttempt> PullAsync(
        string image,
        CancellationToken cancellationToken)
    {
        ProcessResult result = await RunAsync(["image", "pull", image], cancellationToken);
        return new ImagePullAttempt(result.ExitCode == 0, result.StandardOutput, result.StandardError);
    }

    private static async Task<ProcessResult> RunAsync(
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
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Docker process could not be started");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new ProcessResult(
            process.ExitCode,
            await outputTask,
            await errorTask);
    }

    private static string SelectError(ProcessResult result) =>
        string.IsNullOrWhiteSpace(result.StandardError)
            ? $"docker exited with code {result.ExitCode}: {result.StandardOutput.Trim()}"
            : result.StandardError.Trim();

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
