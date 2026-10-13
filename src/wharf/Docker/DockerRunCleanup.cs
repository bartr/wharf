using System.Diagnostics;
using System.Collections.Concurrent;

namespace Wharf.Docker;

public static class DockerRunCleanup
{
    private static readonly ConcurrentDictionary<string, byte> OwnedImages =
        new(StringComparer.Ordinal);

    public static void RegisterImage(string image)
    {
        if (!string.IsNullOrWhiteSpace(image))
        {
            OwnedImages.TryAdd(image, 0);
        }
    }

    public static void ForgetImage(string image) =>
        OwnedImages.TryRemove(image, out _);

    public static void CleanupAll()
    {
        try
        {
            string containerIds = RunDocker(
                ["ps", "-aq", "--filter", "label=com.bartr.wharf.managed=true"]);
            if (!string.IsNullOrWhiteSpace(containerIds))
            {
                RunDocker(["rm", "--force", .. containerIds
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)]);
            }

            string[] ownedImages = OwnedImages.Keys
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (ownedImages.Length > 0)
            {
                RunDocker(["image", "rm", "--force", .. ownedImages]);
                foreach (string image in ownedImages)
                {
                    ForgetImage(image);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Docker cleanup failed: {exception.Message}");
        }
    }

    private static string RunDocker(IReadOnlyList<string> arguments)
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
            ?? throw new IOException("Docker cleanup process could not be started");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(error) ? output.Trim() : error.Trim());
        return output;
    }
}
