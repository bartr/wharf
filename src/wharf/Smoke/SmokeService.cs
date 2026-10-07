using System.Text.Json;
using Wharf.Host;
using Wharf.Logging;
using Wharf.Tasks;

namespace Wharf.Smoke;

public sealed record SmokeRunRequest(
    string TaskRoot,
    HarborTask Task,
    string OutputDirectory);

public sealed record SmokeRunResult(
    string Status,
    string TaskId,
    string TaskRoot,
    string OutputDirectory);

public interface ISmokeRunExecutor
{
    SmokeRunResult Run(SmokeRunRequest request, WharfLogger logger);
}

public sealed class NoOpSmokeRunExecutor : ISmokeRunExecutor
{
    public SmokeRunResult Run(SmokeRunRequest request, WharfLogger logger)
    {
        logger.Info($"run selected task {request.Task.TaskId} (execution is currently a no-op)");
        logger.Verbose($"run task root: {request.TaskRoot}", 2);
        logger.Verbose($"run output directory: {request.OutputDirectory}", 2);
        return new SmokeRunResult(
            "run-no-op",
            request.Task.TaskId,
            request.TaskRoot,
            request.OutputDirectory);
    }
}

public sealed record SmokeResult(
    HostCheckReport Host,
    string Status,
    string TaskId,
    string TaskRoot,
    string TaskManifestPath,
    string OutputDirectory,
    int ExitCode);

public sealed class SmokeService
{
    private readonly HarborTaskLoader taskLoader;
    private readonly ISmokeRunExecutor runExecutor;

    public SmokeService(
        HarborTaskLoader? taskLoader = null,
        ISmokeRunExecutor? runExecutor = null)
    {
        this.taskLoader = taskLoader ?? new HarborTaskLoader();
        this.runExecutor = runExecutor ?? new NoOpSmokeRunExecutor();
    }

    public SmokeResult Run(
        string taskRoot,
        string outputDirectory,
        HostCheckReport host,
        WharfLogger logger)
    {
        if (string.IsNullOrWhiteSpace(taskRoot) || !Directory.Exists(taskRoot))
            throw new SmokeException($"smoke task directory does not exist: {taskRoot}");
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new SmokeException("smoke output directory is required");

        string resolvedTaskRoot = Path.GetFullPath(taskRoot);
        string resolvedOutput = Path.GetFullPath(outputDirectory);
        logger.Info($"smoke validating task directory {resolvedTaskRoot}");
        IReadOnlyList<HarborTask> tasks = taskLoader.Load(resolvedTaskRoot);
        if (tasks.Count == 0)
            throw new SmokeException($"smoke task directory contains no task.toml files: {resolvedTaskRoot}");

        HarborTask task = tasks.OrderBy(item => item.TaskId, StringComparer.Ordinal).First();
        logger.Verbose($"smoke discovered {tasks.Count} task(s)", 1);
        logger.Info($"smoke selected task {task.TaskId}");
        logger.Verbose($"task manifest: {Path.Combine(task.RootPath, "task.toml")}", 2);
        logger.Verbose($"task checksum: {task.SourceChecksum}", 2);
        logger.Verbose($"instruction: {task.InstructionPath}", 3);
        logger.Verbose($"resources: cpus={task.Cpus}; memory_mb={task.MemoryMb}; storage_mb={task.StorageMb}", 3);

        Directory.CreateDirectory(resolvedOutput);
        SmokeRunResult run = runExecutor.Run(
            new SmokeRunRequest(resolvedTaskRoot, task, resolvedOutput),
            logger);
        File.WriteAllText(
            Path.Combine(resolvedOutput, "smoke-selection.json"),
            JsonSerializer.Serialize(new
            {
                taskRoot = resolvedTaskRoot,
                taskId = task.TaskId,
                taskManifest = Path.Combine(task.RootPath, "task.toml"),
                status = run.Status
            }, JsonOptions));

        string status = host.IsSuccess ? run.Status : "qualification-blocked";
        return new SmokeResult(
            host,
            status,
            task.TaskId,
            resolvedTaskRoot,
            Path.Combine(task.RootPath, "task.toml"),
            resolvedOutput,
            host.IsSuccess ? 0 : host.ExitCode);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}

public sealed class SmokeException(string message) : Exception(message);
