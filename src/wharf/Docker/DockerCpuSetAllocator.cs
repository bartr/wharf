using System.Globalization;
using Wharf.Agent;

namespace Wharf.Docker;

internal sealed class DockerCpuSetAllocator(
    Func<string, CancellationToken, Task<string>>? discover = null) : IDisposable
{
    private readonly SemaphoreSlim discoveryGate = new(1, 1);
    private int[]? availableCpus;

    public async Task<string?> AllocateAsync(
        string image,
        int? limit,
        int trialIndex,
        CancellationToken cancellationToken)
    {
        if (limit is null)
            return null;
        if (limit <= 0 || trialIndex < 0)
            throw new AgentExecutionException("CPU limit must be positive and trial index non-negative");

        await discoveryGate.WaitAsync(cancellationToken);
        try
        {
            availableCpus ??= ParseCpuList(
                await (discover ?? DiscoverAsync)(image, cancellationToken));
            return SelectCpuSet(availableCpus, limit.Value, trialIndex);
        }
        finally
        {
            discoveryGate.Release();
        }
    }

    internal static string? SelectCpuSet(IReadOnlyList<int> available, int limit, int trialIndex)
    {
        if (available.Count == 0 || limit <= 0 || trialIndex < 0)
            throw new AgentExecutionException("CPU allocation requires available CPUs, a positive limit, and a non-negative trial index");
        if (available.Count <= limit)
            return null;

        int start = (int)((long)trialIndex * limit % available.Count);
        return string.Join(",", Enumerable.Range(0, limit)
            .Select(offset => available[(int)(((long)start + offset) % available.Count)])
            .Order()
            .Select(cpu => cpu.ToString(CultureInfo.InvariantCulture)));
    }

    internal static int[] ParseCpuList(string value)
    {
        var cpus = new HashSet<int>();
        foreach (string part in value.Trim().Split(','))
        {
            string[] bounds = part.Split('-');
            if (bounds.Length is < 1 or > 2 ||
                !int.TryParse(bounds[0], NumberStyles.None, CultureInfo.InvariantCulture, out int first) ||
                (bounds.Length == 2 &&
                    !int.TryParse(bounds[1], NumberStyles.None, CultureInfo.InvariantCulture, out _)))
            {
                throw new AgentExecutionException($"invalid Docker CPU list: {value.Trim()}");
            }

            int last = bounds.Length == 2
                ? int.Parse(bounds[1], CultureInfo.InvariantCulture)
                : first;
            if (last < first || (long)last - first >= int.MaxValue)
                throw new AgentExecutionException($"invalid Docker CPU range: {part}");
            for (long cpu = first; cpu <= last; cpu++)
            {
                if (!cpus.Add((int)cpu))
                    throw new AgentExecutionException($"duplicate Docker CPU ID: {cpu}");
            }
        }
        return cpus.Order().ToArray();
    }

    private static async Task<string> DiscoverAsync(string image, CancellationToken cancellationToken)
    {
        string name = $"agent-cpu-probe-{Guid.NewGuid():N}";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            await RunDockerAsync(
                [
                    "create", "--name", name,
                    "--label", "com.bartr.wharf.managed=true",
                    "--network", "none", "--memory", "8g", "--pids-limit", "1024",
                    "--entrypoint", "/bin/bash", image,
                    "-lc", "cat /proc/self/status"
                ],
                timeout.Token);
            string status = await RunDockerAsync(["start", "--attach", name], timeout.Token);
            const string prefix = "Cpus_allowed_list:";
            string? line = status.Split('\n')
                .SingleOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal));
            return line is null
                ? throw new AgentExecutionException("Docker CPU discovery did not report Cpus_allowed_list")
                : line[prefix.Length..].Trim();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AgentExecutionException("Docker CPU discovery exceeded its 30-second timeout");
        }
        finally
        {
            using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                DockerProcessResult removal = await DockerAgentProcess.RunAsync(
                    ["rm", "--force", name], null, null, cleanupTimeout.Token);
                if (removal.ExitCode != 0 &&
                    !removal.StandardError.Contains("No such container", StringComparison.OrdinalIgnoreCase))
                {
                    throw new AgentExecutionException(
                        $"Docker CPU probe cleanup failed: {removal.StandardError.Trim()}");
                }
            }
            catch (OperationCanceledException) when (cleanupTimeout.IsCancellationRequested)
            {
                throw new AgentExecutionException("Docker CPU probe cleanup exceeded its 30-second timeout");
            }
        }
    }

    private static async Task<string> RunDockerAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        DockerProcessResult result = await DockerAgentProcess.RunAsync(
            arguments, null, null, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new AgentExecutionException(
                $"Docker CPU discovery failed: exit={result.ExitCode} " +
                $"{result.StandardError.Trim()} {result.StandardOutput.Trim()}");
        }
        return result.StandardOutput;
    }

    public void Dispose() => discoveryGate.Dispose();
}
