using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Wharf.ExitCodes;

namespace Wharf.Host;

public sealed record HostCheck(
    string Name,
    bool Passed,
    bool Overridable,
    string Detail,
    bool Overridden = false);

public sealed record HostCheckReport(IReadOnlyList<HostCheck> Checks)
{
    public bool IsSuccess => Checks.All(check => check.Passed);
    public bool HasUnsupportedHost => Checks.Any(
        check => check.Name == "platform" && (!check.Passed || check.Overridden));
    public bool HasBlockingFailure => Checks.Any(check => !check.Passed && !check.Overridable);
    public int ExitCode => IsSuccess ? Wharf.ExitCodes.ExitCode.Success.Value :
        HasBlockingFailure ? Wharf.ExitCodes.ExitCode.BlockedRun.Value : Wharf.ExitCodes.ExitCode.UnsupportedHost.Value;
}

public sealed record HostCheckOptions(
    bool AllowUnsupportedHost = false,
    long MinimumMemoryBytes = 8L * 1024 * 1024 * 1024,
    long MinimumDiskBytes = 50L * 1024 * 1024 * 1024,
    long MinimumCpuCount = 4,
    long MinimumNofileSoft = 65_536,
    long MinimumNofileHard = 65_536,
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
                ? check with { Passed = true, Overridden = true, Detail = $"OVERRIDDEN: {check.Detail}" }
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
        (bool wsl, string wslSource) = IsWsl();
        string detail = $"ubuntu24.04={ubuntu}; amd64={amd64}; wsl={wsl} ({wslSource})";
        return new HostCheck("platform", supported, true, detail);
    }

    private static (bool Detected, string Source) IsWsl()
    {
        string? explicitValue = Environment.GetEnvironmentVariable("WHARF_WSL");
        if (explicitValue is "1" or "true" or "yes")
        {
            return (true, "explicit");
        }

        if (Environment.GetEnvironmentVariable("WSL_INTEROP") is not null ||
            Environment.GetEnvironmentVariable("WSL_DISTRO_NAME") is not null)
        {
            return (true, "environment");
        }

        if (File.Exists("/proc/version") &&
            File.ReadAllText("/proc/version").Contains("microsoft", StringComparison.OrdinalIgnoreCase))
        {
            return (true, "kernel");
        }

        return (false, "not detected");
    }
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
            Task<string> errorTask = process.StandardError.ReadToEndAsync();
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            process.WaitForExit();
            string error = errorTask.GetAwaiter().GetResult();
            string output = outputTask.GetAwaiter().GetResult();
            if (process.ExitCode != 0)
            {
                return new HostCheck("docker", false, false, string.IsNullOrWhiteSpace(error)
                    ? $"docker exited with code {process.ExitCode}"
                    : error.Trim());
            }

            using JsonDocument document = JsonDocument.Parse(output);
            JsonElement root = document.RootElement;
            if (!HasBoolean(root, "MemoryLimit") ||
                !HasBoolean(root, "PidsLimit") ||
                !HasNetworkPlugin(root))
            {
                return new HostCheck(
                    "docker",
                    false,
                    false,
                    "Docker lacks required memory, PID, or bridge-network capabilities");
            }

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

    private static bool HasBoolean(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.True;

    private static bool HasNetworkPlugin(JsonElement root) =>
        root.TryGetProperty("Plugins", out JsonElement plugins) &&
        plugins.TryGetProperty("Network", out JsonElement networks) &&
        networks.ValueKind == JsonValueKind.Array &&
        networks.EnumerateArray().Any(item =>
            item.ValueKind == JsonValueKind.String &&
            item.GetString() == "bridge");
}

public sealed class CapacityProbe : IHostProbe
{
    public HostCheck Check(HostCheckOptions options)
    {
        try
        {
            long memory = ReadMemAvailable();
            long disk = new DriveInfo("/").AvailableFreeSpace;
            long cpu = Environment.ProcessorCount;
            (long nofileSoft, long nofileHard) = ReadNofileLimits();
            long inotify = ReadLong("/proc/sys/fs/inotify/max_user_instances");
            long inotifyWatches = ReadLong("/proc/sys/fs/inotify/max_user_watches");
            long pids = ReadLong("/proc/sys/kernel/pid_max");
            bool passed = memory >= options.MinimumMemoryBytes &&
                disk >= options.MinimumDiskBytes &&
                cpu >= options.MinimumCpuCount &&
                nofileSoft >= options.MinimumNofileSoft &&
                nofileHard >= options.MinimumNofileHard &&
                inotify >= options.MinimumInotifyInstances &&
                inotifyWatches >= options.MinimumInotifyWatches &&
                pids >= options.MinimumPids;
            return new HostCheck(
                "capacity",
                passed,
                false,
                $"memory={memory}; disk={disk}; cpu={cpu}; nofileSoft={nofileSoft}; nofileHard={nofileHard}; inotifyInstances={inotify}; inotifyWatches={inotifyWatches}; pids={pids}");
        }
        catch (IOException exception)
        {
            return new HostCheck("capacity", false, false, $"capacity probe failed: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return new HostCheck("capacity", false, false, $"capacity probe failed: {exception.Message}");
        }
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

    private static (long Soft, long Hard) ReadNofileLimits()
    {
        string? line = File.ReadLines("/proc/self/limits")
            .FirstOrDefault(value => value.StartsWith("Max open files", StringComparison.Ordinal));
        if (line is null)
        {
            return (0, 0);
        }

        string[] fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return fields.Length >= 5 &&
               long.TryParse(fields[^3], out long soft) &&
               long.TryParse(fields[^2], out long hard)
            ? (soft, hard)
            : (0, 0);
    }

    private static long ReadLong(string path) =>
            File.Exists(path) && long.TryParse(File.ReadAllText(path).Trim(), out long value) ? value : 0;
}
