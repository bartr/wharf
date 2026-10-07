using System.Text.Json;
using Tomlyn;
using Tomlyn.Model;
using Wharf.Configuration;
using Wharf.Image;
using Wharf.Logging;
using Wharf.Tasks;

namespace Wharf.Run;

public sealed record RunResult(
    string Status,
    string DatasetRoot,
    string? DatasetManifest,
    int TaskCount,
    string OutputDirectory);

public sealed class RunService
{
    private readonly HarborTaskLoader taskLoader;
    private readonly IAgentImagePreparer imagePreparer;

    public RunService(
        HarborTaskLoader? taskLoader = null,
        IAgentImagePreparer? imagePreparer = null)
    {
        this.taskLoader = taskLoader ?? new HarborTaskLoader();
        this.imagePreparer = imagePreparer ?? new DockerAgentImagePreparer();
    }

    public RunResult Execute(
        string taskRoot,
        string outputDirectory,
        RunConfiguration configuration,
        WharfLogger logger)
    {
        string resolvedRoot = Path.GetFullPath(taskRoot);
        if (!Directory.Exists(resolvedRoot))
            throw new RunException($"run task directory does not exist: {resolvedRoot}");

        string datasetPath = Path.Combine(resolvedRoot, "dataset.toml");
        string? datasetManifest = File.Exists(datasetPath) ? datasetPath : null;
        if (datasetManifest is not null)
        {
            try
            {
                TomlTable dataset = Toml.ToModel(File.ReadAllText(datasetManifest));
                if (!dataset.ContainsKey("dataset"))
                    throw new RunException($"dataset manifest is missing [dataset]: {datasetManifest}");
            }
            catch (TomlException exception)
            {
                throw new RunException($"invalid dataset manifest: {datasetManifest}: {exception.Message}");
            }
        }

        IReadOnlyList<HarborTask> tasks = taskLoader.Load(resolvedRoot);
        if (tasks.Count == 0)
            throw new RunException($"run task directory contains no task.toml files: {resolvedRoot}");

        string resolvedOutput = Path.GetFullPath(outputDirectory);
        string jobDirectory = Path.Combine(
            resolvedOutput,
            $"{RunConfigurationResolver.DefaultCopilotCli}-" +
            $"{configuration.Model}-max-{configuration.CliVersion}");
        Directory.CreateDirectory(jobDirectory);
        string attemptDirectory = CreateAttemptDirectory(jobDirectory);
        logger.Info($"run discovered {tasks.Count} task(s)");
        logger.Info($"run created {attemptDirectory} under {jobDirectory}");

        Parallel.ForEach(
            tasks,
            new ParallelOptions { MaxDegreeOfParallelism = configuration.B },
            task =>
        {
            string trialDirectory = Path.Combine(
                jobDirectory,
                attemptDirectory,
                $"{SanitizePathPart(task.TaskId)}__{task.SourceChecksum[..8]}");
            Directory.CreateDirectory(Path.Combine(trialDirectory, "agent"));
            Directory.CreateDirectory(Path.Combine(trialDirectory, "artifacts"));
            Directory.CreateDirectory(Path.Combine(trialDirectory, "verifier"));
            string image = imagePreparer.PrepareAsync(
                task,
                Path.Combine(trialDirectory, "agent"),
                logger.Info,
                CancellationToken.None).GetAwaiter().GetResult();
            logger.Info($"running agent task {task.TaskId}");
            logger.Info("agent execution pending");
            logger.Info($"running verifier task {task.TaskId}");
            logger.Info("verifier execution pending");
            logger.Info($"cleaning up task {task.TaskId}");
            logger.Info($"finishing task {task.TaskId}");
            logger.Info($"complete task={task.TaskId} result=execution-pending");
            var result = new
            {
                status = "run-no-op",
                taskRoot = resolvedRoot,
                taskId = task.TaskId,
                taskManifest = Path.Combine(task.RootPath, "task.toml"),
                datasetManifest,
                agentImage = image
            };
            File.WriteAllText(
                Path.Combine(trialDirectory, "result.json"),
                JsonSerializer.Serialize(result, JsonOptions));
        });

        return new RunResult(
            "run-no-op",
            resolvedRoot,
            datasetManifest,
            tasks.Count,
            resolvedOutput);
    }

    private static string CreateAttemptDirectory(string jobDirectory)
    {
        for (int attempt = 1; ; attempt++)
        {
            string name = $"all-{attempt}";
            string path = Path.Combine(jobDirectory, name);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                return name;
            }
        }
    }

    private static string SanitizePathPart(string value)
    {
        var invalidCharacters = new HashSet<char>(Path.GetInvalidFileNameChars());
        return new string(value.Select(character =>
            invalidCharacters.Contains(character) ? '_' : character).ToArray());
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}

public sealed class RunException(string message) : Exception(message);
