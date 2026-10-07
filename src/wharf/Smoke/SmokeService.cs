using System.Text.Json;
using Tomlyn;
using Tomlyn.Model;
using Wharf.Configuration;
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
        WharfLogger logger,
        string? include = null)
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

        HarborTask task = SelectTask(resolvedTaskRoot, tasks, include);
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
        WriteSmokeArtifacts(resolvedOutput, resolvedTaskRoot, task, run);

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

    private static void WriteSmokeArtifacts(
        string outputDirectory,
        string taskRoot,
        HarborTask task,
        SmokeRunResult run)
    {
        string jobDirectory = Path.Combine(
            outputDirectory,
            $"{RunConfigurationResolver.DefaultCopilotCli}-" +
            $"{RunConfigurationResolver.DefaultCopilotModel}-max-" +
            $"{RunConfigurationResolver.DefaultCopilotVersion}");
        Directory.CreateDirectory(jobDirectory);
        string attemptDirectory = CreateAttemptDirectory(jobDirectory);
        string trialDirectory = Path.Combine(
            jobDirectory,
            attemptDirectory,
            $"{SanitizePathPart(task.TaskId)}__{task.SourceChecksum[..8]}");
        string artifactsDirectory = Path.Combine(trialDirectory, "artifacts");
        Directory.CreateDirectory(Path.Combine(trialDirectory, "agent"));
        Directory.CreateDirectory(artifactsDirectory);
        Directory.CreateDirectory(Path.Combine(trialDirectory, "verifier"));

        var selection = new
        {
            taskRoot,
            taskId = task.TaskId,
            taskManifest = Path.Combine(task.RootPath, "task.toml"),
            status = run.Status
        };
        File.WriteAllText(
            Path.Combine(trialDirectory, "smoke-selection.json"),
            JsonSerializer.Serialize(selection, JsonOptions));
        File.WriteAllText(
            Path.Combine(trialDirectory, "result.json"),
            JsonSerializer.Serialize(selection, JsonOptions));
        File.WriteAllText(
            Path.Combine(artifactsDirectory, "manifest.json"),
            JsonSerializer.Serialize(Array.Empty<object>(), JsonOptions));
    }

    private static string SanitizePathPart(string value)
    {
        var invalidCharacters = new HashSet<char>(Path.GetInvalidFileNameChars());
        return new string(value.Select(character =>
            invalidCharacters.Contains(character) ? '_' : character).ToArray());
    }

    private static HarborTask SelectTask(
        string taskRoot,
        IReadOnlyList<HarborTask> tasks,
        string? include)
    {
        string? datasetPath = Path.Combine(taskRoot, "dataset.toml");
        if (File.Exists(datasetPath))
        {
            TomlTable dataset;
            try
            {
                dataset = Toml.ToModel(File.ReadAllText(datasetPath));
            }
            catch (TomlException exception)
            {
                throw new SmokeException($"invalid smoke dataset manifest: {datasetPath}: {exception.Message}");
            }

            if (dataset.TryGetValue("tasks", out object? value) &&
                value is TomlTableArray entries)
            {
                TomlTable? selected = entries
                    .Where(entry => entry.TryGetValue("name", out object? name) && name is string)
                    .FirstOrDefault(entry => include is null ||
                        MatchesInclude((string)entry["name"], include));
                if (selected is not null &&
                    selected.TryGetValue("name", out object? nameValue) &&
                    nameValue is string name)
                {
                    string taskId = name.Split('/').Last();
                    HarborTask? task = tasks.FirstOrDefault(item =>
                        string.Equals(item.TaskId, taskId, StringComparison.Ordinal) ||
                        string.Equals(Path.GetFileName(item.RootPath), taskId, StringComparison.Ordinal));
                    if (task is not null)
                        return task;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(include))
        {
            HarborTask? included = tasks.FirstOrDefault(item =>
                MatchesInclude(item.TaskId, include) ||
                MatchesInclude(Path.GetFileName(item.RootPath), include));
            if (included is not null)
                return included;
            throw new SmokeException($"smoke task include did not match a task: {include}");
        }

        return tasks.OrderBy(item => item.TaskId, StringComparer.Ordinal).First();
    }

    private static bool MatchesInclude(string value, string include) =>
        string.Equals(value, include, StringComparison.OrdinalIgnoreCase) ||
        value.EndsWith('/' + include, StringComparison.OrdinalIgnoreCase);

    private static string CreateAttemptDirectory(string jobDirectory)
    {
        for (int attempt = 1; ; attempt++)
        {
            string name = $"smoke-{attempt}";
            string path = Path.Combine(jobDirectory, name);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                return name;
            }
        }
    }
}

public sealed class SmokeException(string message) : Exception(message);
