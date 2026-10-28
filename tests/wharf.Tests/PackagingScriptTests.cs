using System.Diagnostics;

namespace Wharf.Tests;

public sealed class PackagingScriptTests
{
    [Fact]
    public async Task PackagedExecutableRunsWithRequestedVersion()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string directory = Path.Combine(Path.GetTempPath(), "wharf-package-" + Guid.NewGuid().ToString("N"));
        string output = Path.Combine(directory, "package");
        string extracted = Path.Combine(directory, "extracted");
        const string version = "7.8.9";
        try
        {
            await RunAsync("bash", Path.Combine(root, "scripts/package-wharf.sh"), version, output);
            await RunAsync("dpkg-deb", "--extract",
                Path.Combine(output, $"wharf_{version}_amd64.deb"), extracted);

            string packagedVersion = await RunAsync(
                Path.Combine(extracted, "usr/bin/wharf"), "--version");

            Assert.Equal(version + Environment.NewLine, packagedVersion);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void PackagingScriptsAreExecutableAndUseExplicitPrivilegeBoundaries()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string package = Path.Combine(root, "scripts/package-wharf.sh");
        string bootstrap = Path.Combine(root, "scripts/wharf-bootstrap.sh");
        string rollback = Path.Combine(root, "scripts/wharf-rollback.sh");

        Assert.True(File.Exists(package));
        Assert.True(File.Exists(bootstrap));
        Assert.True(File.Exists(rollback));
        Assert.Contains("dpkg-deb --build", File.ReadAllText(package));
        Assert.Contains("SIGNING_KEY", File.ReadAllText(package));
        Assert.Contains("exec sudo", File.ReadAllText(bootstrap));
        Assert.Contains("exec sudo", File.ReadAllText(rollback));
    }

    private static async Task<string> RunAsync(string executable, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {executable}");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        string output = await stdout;
        string error = await stderr;
        Assert.True(process.ExitCode == 0,
            $"{executable} exited {process.ExitCode}: {error}{Environment.NewLine}{output}");
        return output;
    }
}
