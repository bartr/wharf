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
        int plannedRuns = checked(tasks.Count * configuration.K);
        logger.Info($"plan tasks={tasks.Count} attempts={configuration.K} runs={plannedRuns}");
        logger.Info($"run created {attemptDirectory} under {jobDirectory}");

        Parallel.ForEach(
            tasks.Select((task, index) => (task, index)),
            new ParallelOptions { MaxDegreeOfParallelism = configuration.B },
            item =>
        {
            HarborTask task = item.task;
            int runNumber = (item.index * configuration.K) + 1;
            string prefix = FormatRunPrefix(runNumber, plannedRuns, task, 1, configuration.K);
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
                message => logger.Info($"{prefix} status={message}"),
                CancellationToken.None).GetAwaiter().GetResult();
            logger.Info($"{prefix} status=running-agent");
            logger.Info($"{prefix} status=agent-execution-pending");
            logger.Info($"{prefix} status=running-verifier");
            logger.Info($"{prefix} status=verifier-execution-pending");
            logger.Info($"{prefix} status=cleaning-up");
            logger.Info($"{prefix} status=complete result=execution-pending");
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

    private static string FormatRunPrefix(
        int runNumber,
        int totalRuns,
        HarborTask task,
        int attempt,
        int attempts) =>
        $"run={runNumber:D3}/{totalRuns:D3} task={ShortTaskName(task.TaskId)} " +
        $"attempt={attempt}/{attempts}";

    private static string ShortTaskName(string taskId) =>
        taskId.Length <= 32 ? taskId : taskId[..32];

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
