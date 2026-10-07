using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Wharf.ExitCodes;

namespace Wharf.Host;

public sealed record HostCheck(
    string Name,
    bool Passed,
    bool Overridable,
    string Detail);

public sealed record HostCheckReport(IReadOnlyList<HostCheck> Checks)
{
    public bool IsSuccess => Checks.All(check => check.Passed);
    public bool HasUnsupportedHost => Checks.Any(check => check.Name == "platform" && !check.Passed);
    public bool HasBlockingFailure => Checks.Any(check => !check.Passed && !check.Overridable);
    public int ExitCode => IsSuccess ? Wharf.ExitCodes.ExitCode.Success.Value :
        HasBlockingFailure ? Wharf.ExitCodes.ExitCode.BlockedRun.Value : Wharf.ExitCodes.ExitCode.UnsupportedHost.Value;
}

public sealed record HostCheckOptions(
    bool AllowUnsupportedHost = false,
    long MinimumMemoryBytes = 8L * 1024 * 1024 * 1024,
    long MinimumDiskBytes = 50L * 1024 * 1024 * 1024,
    long MinimumFileDescriptors = 65_536,
    long MinimumInotifyInstances = 1_024,
    long MinimumInotifyWatches = 65_536,
    long MinimumPids = 32_768);

public interface IHostProbe
{
    HostCheck Check(HostCheckOptions options);
}

public sealed class HostCheckService(IEnumerable<IHostProbe> probes)
{
    public HostCheckReport Check(HostCheckOptions options)
    {
        HostCheck[] checks = probes.Select(probe => probe.Check(options)).ToArray();
        if (options.AllowUnsupportedHost)
        {
            checks = checks.Select(check => check.Name == "platform" && !check.Passed
                ? check with { Passed = true, Detail = $"OVERRIDDEN: {check.Detail}" }
                : check).ToArray();
        }

        return new HostCheckReport(checks);
    }
}

public sealed class PlatformProbe : IHostProbe
{
    public HostCheck Check(HostCheckOptions options)
    {
        string release = File.Exists("/etc/os-release") ? File.ReadAllText("/etc/os-release") : string.Empty;
        bool ubuntu = release.Contains("ID=ubuntu", StringComparison.OrdinalIgnoreCase) &&
            release.Contains("VERSION_ID=\"24.04\"", StringComparison.Ordinal);
        bool amd64 = RuntimeInformation.OSArchitecture == Architecture.X64;
        bool supported = ubuntu && amd64;
        string detail = $"ubuntu24.04={ubuntu}; amd64={amd64}; wsl={IsWsl()}";
        return new HostCheck("platform", supported, true, detail);
    }

    private static bool IsWsl() =>
        Environment.GetEnvironmentVariable("WSL_INTEROP") is not null ||
        (File.Exists("/proc/version") &&
         File.ReadAllText("/proc/version").Contains("microsoft", StringComparison.OrdinalIgnoreCase));
}

public sealed class DockerProbe : IHostProbe
{
    public HostCheck Check(HostCheckOptions options)
    {
        ProcessStartInfo startInfo = new("docker")
        {
            ArgumentList = { "info", "--format", "{{json .}}" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Docker process could not be started");
            string error = process.StandardError.ReadToEnd();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                return new HostCheck("docker", false, false, error.Trim());
            }

            using JsonDocument document = JsonDocument.Parse(output);
            return new HostCheck("docker", true, false, "Docker daemon is reachable");
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            return new HostCheck("docker", false, false, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return new HostCheck("docker", false, false, exception.Message);
        }
        catch (JsonException exception)
        {
            return new HostCheck("docker", false, false, $"invalid Docker response: {exception.Message}");
        }
    }
}

public sealed class CapacityProbe : IHostProbe
{
    public HostCheck Check(HostCheckOptions options)
    {
        long memory = ReadMemAvailable();
        long disk = new DriveInfo("/").AvailableFreeSpace;
        long fileDescriptors = ReadLong("/proc/sys/fs/file-max");
        long inotify = ReadLong("/proc/sys/fs/inotify/max_user_instances");
        long inotifyWatches = ReadLong("/proc/sys/fs/inotify/max_user_watches");
        long pids = ReadLong("/proc/sys/kernel/pid_max");
        bool passed = memory >= options.MinimumMemoryBytes &&
            disk >= options.MinimumDiskBytes &&
            fileDescriptors >= options.MinimumFileDescriptors &&
            inotify >= options.MinimumInotifyInstances &&
            inotifyWatches >= options.MinimumInotifyWatches &&
            pids >= options.MinimumPids;
        return new HostCheck(
            "capacity",
            passed,
            false,
            $"memory={memory}; disk={disk}; fileDescriptors={fileDescriptors}; inotifyInstances={inotify}; inotifyWatches={inotifyWatches}; pids={pids}");
    }

    private static long ReadMemAvailable()
    {
        string? line = File.ReadLines("/proc/meminfo")
            .FirstOrDefault(value => value.StartsWith("MemAvailable:", StringComparison.Ordinal));
        return line is not null && long.TryParse(
            line["MemAvailable:".Length..].Trim().TrimEnd('k', 'B'),
            out long kilobytes)
            ? kilobytes * 1024
            : 0;
    }

    private static long ReadLong(string path) =>
        long.TryParse(File.ReadAllText(path).Trim(), out long value) ? value : 0;
}
