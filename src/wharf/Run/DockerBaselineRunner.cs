using System.Collections.Concurrent;
using System.Diagnostics;
using Wharf.Agent;

namespace Wharf.Run;

internal sealed record BaselineRequest(
    string TaskId,
    string Image,
    string ContainerName,
    string EvidenceDirectory,
    string ArtifactsDirectory,
    string WorkspaceDirectory,
    string? SolutionDirectory,
    TimeSpan Timeout,
    bool RetainWorkspace,
    string? CpuSet);

internal sealed record BaselineCommandResult(
    string Step,
    int ExitCode,
    long DurationMilliseconds,
    bool TimedOut);

internal interface IBaselineRunner
{
    Task PrepareAsync(BaselineRequest request, CancellationToken cancellationToken);
    Task<BaselineCommandResult> ExecuteAsync(
        BaselineRequest request, string step, CancellationToken cancellationToken);
    Task CleanupAsync(BaselineRequest request);
}

internal sealed class DockerBaselineRunner : IBaselineRunner
{
    private readonly ConcurrentDictionary<string, byte> containers = new(StringComparer.Ordinal);

    public async Task PrepareAsync(BaselineRequest request, CancellationToken cancellationToken)
    {
        await RunCheckedAsync(BuildCreateArguments(request), cancellationToken);
        containers.TryAdd(request.ContainerName, 0);
        await RunCheckedAsync(["start", request.ContainerName], cancellationToken);
        await RunCheckedAsync(
            [
                "exec", "--user", "0:0", request.ContainerName, "/bin/bash", "-lc",
                "mkdir -p /logs/baseline /workspace && " +
                "cp -a /app/. /workspace/ && " +
                "chown -R 1000:1000 /workspace /logs/baseline"
            ],
            cancellationToken);
        if (request.SolutionDirectory is not null)
        {
            await RunCheckedAsync(
                ["exec", "--user", "0:0", request.ContainerName, "mkdir", "-p", "/solution"],
                cancellationToken);
            await RunCheckedAsync(
                ["cp", $"{request.SolutionDirectory}/.", $"{request.ContainerName}:/solution/"],
                cancellationToken);
            await RunCheckedAsync(
                [
                    "exec", "--user", "0:0", request.ContainerName, "/bin/bash", "-lc",
                    """
                    set -euo pipefail
                    cd /app
                    git config --global --add safe.directory /app
                    if ! git rev-parse --git-dir >/dev/null 2>&1; then git init -q; fi
                    git config user.name Wharf
                    git config user.email wharf@localhost
                    git add -A
                    git -c core.hooksPath=/dev/null commit --allow-empty --no-verify -qm wharf-oracle-base
                    git rev-parse HEAD >/logs/baseline/base-commit
                    """,
                ],
                cancellationToken);
        }
    }

    public async Task<BaselineCommandResult> ExecuteAsync(
        BaselineRequest request, string step, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> arguments = BuildExecutionArguments(request, step);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout);
        var duration = Stopwatch.StartNew();
        bool timedOut = false;
        int exitCode;
        try
        {
            DockerProcessResult result = await DockerAgentProcess.RunAsync(
                arguments, null, null, timeout.Token);
            exitCode = result.ExitCode;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            exitCode = 124;
            await DockerContainerControl.StopAsync(request.ContainerName, CancellationToken.None);
        }
        duration.Stop();
        if (step == "oracle" && !timedOut)
        {
            Directory.CreateDirectory(request.ArtifactsDirectory);
            await RunCheckedAsync(
                ["cp", $"{request.ContainerName}:/logs/baseline/model.patch",
                    Path.Combine(request.ArtifactsDirectory, "model.patch")],
                CancellationToken.None);
        }
        return new BaselineCommandResult(step, exitCode, duration.ElapsedMilliseconds, timedOut);
    }

    public async Task CleanupAsync(BaselineRequest request)
    {
        if (!containers.TryRemove(request.ContainerName, out _))
            return;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            Directory.CreateDirectory(request.EvidenceDirectory);
            await RunCheckedAsync(
                ["cp", $"{request.ContainerName}:/logs/baseline/.", request.EvidenceDirectory],
                timeout.Token);
            if (request.RetainWorkspace)
            {
                Directory.CreateDirectory(request.WorkspaceDirectory);
                string workspace = request.SolutionDirectory is null ? "/workspace" : "/app";
                await RunCheckedAsync(
                    ["cp", $"{request.ContainerName}:{workspace}/.", request.WorkspaceDirectory],
                    timeout.Token);
            }
        }
        finally
        {
            using var removalTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await RunCheckedAsync(["rm", "--force", request.ContainerName], removalTimeout.Token);
        }
    }

    internal static IReadOnlyList<string> BuildCreateArguments(BaselineRequest request) =>
        [
            "create", "--name", request.ContainerName,
            "--label", "com.bartr.wharf.managed=true",
            "--network", "bridge", "--memory", "8g", "--pids-limit", "1024",
            .. request.CpuSet is null ? Array.Empty<string>() : new[] { "--cpuset-cpus", request.CpuSet },
            "--user", "1000:1000", "--entrypoint", "/bin/bash",
            request.Image, "-lc", "exec sleep infinity"
        ];

    internal static IReadOnlyList<string> BuildExecutionArguments(BaselineRequest request, string step)
    {
        string script = step switch
        {
            "no-op" => "true >/logs/baseline/no-op.stdout.log 2>/logs/baseline/no-op.stderr.log",
            "oracle" when request.SolutionDirectory is not null =>
                """
                set -uo pipefail
                cd /app || exit $?
                bash /solution/solve.sh >/logs/baseline/oracle.stdout.log 2>/logs/baseline/oracle.stderr.log
                status=$?
                git add -N . || exit $?
                git diff --binary --no-ext-diff "$(cat /logs/baseline/base-commit)" >/logs/baseline/model.patch || exit $?
                exit "$status"
                """,
            _ => throw new RunException($"unsupported baseline step: {step}")
        };
        return [
            "exec", "--user", step == "oracle" ? "0:0" : "1000:1000",
            request.ContainerName, "/bin/bash", "-lc", script
        ];
    }

    private static async Task RunCheckedAsync(
        IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        DockerProcessResult result = await DockerAgentProcess.RunAsync(
            arguments, null, null, cancellationToken);
        DockerAgentWorkspacePreparer.EnsureSuccessful(result, "baseline Docker operation");
    }
}
