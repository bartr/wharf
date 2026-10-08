using System.Text.Json;
using Tomlyn;
using Tomlyn.Model;
using Wharf.Configuration;
using Wharf.Host;
using Wharf.Logging;
using Wharf.Run;
using Wharf.Tasks;

namespace Wharf.Smoke;

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
    private readonly RunService runService;

    public SmokeService(
        HarborTaskLoader? taskLoader = null,
        RunService? runService = null)
    {
        this.taskLoader = taskLoader ?? new HarborTaskLoader();
        this.runService = runService ?? new RunService(taskLoader);
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
        logger.Info($"smoke selected task {task.TaskId}");
        if (!host.IsSuccess)
        {
            return new SmokeResult(
                host,
                "qualification-blocked",
                task.TaskId,
                resolvedTaskRoot,
                Path.Combine(task.RootPath, "task.toml"),
                resolvedOutput,
                host.ExitCode);
        }

        RunConfiguration configuration = RunConfigurationResolver.Resolve(
            new RunManifest(),
            new RunConfigurationOverrides(
                K: 1,
                N: 1,
                B: 1,
                OutputDirectory: resolvedOutput),
            new Dictionary<string, string?>
            {
                ["MODEL"] = Environment.GetEnvironmentVariable("MODEL"),
                ["WHARF_MODEL"] = Environment.GetEnvironmentVariable("WHARF_MODEL"),
                ["WHARF_EFFORT"] = Environment.GetEnvironmentVariable("WHARF_EFFORT"),
                ["WHARF_API_KEY"] = Environment.GetEnvironmentVariable("WHARF_API_KEY"),
                ["WHARF_TOKEN"] = Environment.GetEnvironmentVariable("WHARF_TOKEN"),
                ["WHARF_PASSWORD"] = Environment.GetEnvironmentVariable("WHARF_PASSWORD")
            });
        RunResult result = runService.Execute(
            resolvedTaskRoot,
            resolvedOutput,
            configuration,
            logger,
            [task],
            "smoke");

        WriteSmokeSelection(resolvedOutput, task, result.Status, resolvedTaskRoot);
        return new SmokeResult(
            host,
            result.Status,
            task.TaskId,
            resolvedTaskRoot,
            Path.Combine(task.RootPath, "task.toml"),
            resolvedOutput,
            0);
    }

    private static void WriteSmokeSelection(
        string outputDirectory,
        HarborTask task,
        string status,
        string taskRoot)
    {
        string jobDirectory = Path.Combine(
            outputDirectory,
            $"{RunConfigurationResolver.DefaultCopilotCli}-" +
            $"{RunConfigurationResolver.DefaultCopilotModel}-max-" +
            $"{RunConfigurationResolver.DefaultCopilotVersion}");
        string? trialDirectory = Directory
            .EnumerateDirectories(jobDirectory, $"{SanitizePathPart(task.TaskId)}__*", SearchOption.AllDirectories)
            .OrderByDescending(path => path, StringComparer.Ordinal)
            .FirstOrDefault();
        if (trialDirectory is null)
            return;

        var selection = new
        {
            taskRoot,
            taskId = task.TaskId,
            taskManifest = Path.Combine(task.RootPath, "task.toml"),
            status
        };
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        File.WriteAllText(
            Path.Combine(trialDirectory, "smoke-selection.json"),
            JsonSerializer.Serialize(selection, options));
    }

    private static HarborTask SelectTask(
        string taskRoot,
        IReadOnlyList<HarborTask> tasks,
        string? include)
    {
        string datasetPath = Path.Combine(taskRoot, "dataset.toml");
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

    private static string SanitizePathPart(string value)
    {
        var invalidCharacters = new HashSet<char>(Path.GetInvalidFileNameChars());
        return new string(value.Select(character =>
            invalidCharacters.Contains(character) ? '_' : character).ToArray());
    }
}

public sealed class SmokeException(string message) : Exception(message);
