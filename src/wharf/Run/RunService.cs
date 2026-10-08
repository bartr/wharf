using System.Text.Json;
using Tomlyn;
using Tomlyn.Model;
using Wharf.Agent;
using Wharf.Configuration;
using Wharf.Copilot;
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
    private readonly AgentExecutionService agentExecution;

    public RunService(
        HarborTaskLoader? taskLoader = null,
        IAgentImagePreparer? imagePreparer = null,
        AgentExecutionService? agentExecution = null)
    {
        this.taskLoader = taskLoader ?? new HarborTaskLoader();
        this.imagePreparer = imagePreparer ?? new DockerAgentImagePreparer();
        this.agentExecution = agentExecution ?? new AgentExecutionService(new DockerAgentRunner());
    }

    public RunResult Execute(
        string taskRoot,
        string outputDirectory,
        RunConfiguration configuration,
        WharfLogger logger,
        IReadOnlyList<HarborTask>? selectedTasks = null,
        string attemptPrefix = "all")
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

        IReadOnlyList<HarborTask> tasks = selectedTasks ?? taskLoader.Load(resolvedRoot);
        if (tasks.Count == 0)
            throw new RunException($"run task directory contains no task.toml files: {resolvedRoot}");

        string resolvedOutput = Path.GetFullPath(outputDirectory);
        string jobDirectory = Path.Combine(
            resolvedOutput,
            $"{RunConfigurationResolver.DefaultCopilotCli}-" +
            $"{configuration.Model}-max-{configuration.CliVersion}");
        Directory.CreateDirectory(jobDirectory);
        string attemptDirectory = CreateAttemptDirectory(jobDirectory, attemptPrefix);
        int plannedRuns = checked(tasks.Count * configuration.K);
        int imageConcurrency = Math.Min(configuration.N, configuration.B);
        string runId = Guid.NewGuid().ToString("N")[..8];
        CopilotInvocation? invocation = null;
        string? invocationError = null;
        try
        {
            invocation = PrepareInvocation(configuration);
        }
        catch (CopilotAdapterException exception)
        {
            invocationError = exception.Message;
            logger.Info($"agent setup failed error={exception.Message}");
        }
        logger.Info(
            $"plan tasks={tasks.Count} attempts={configuration.K} runs={plannedRuns} " +
            $"task-concurrency={configuration.N} image-concurrency={imageConcurrency}");
        logger.Info($"run created {attemptDirectory} under {jobDirectory}");

        Parallel.ForEach(
            tasks.Select((task, index) => (task, index)),
            new ParallelOptions { MaxDegreeOfParallelism = imageConcurrency },
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
            logger.Info($"{prefix} status=preparing-image");
            string image = imagePreparer.PrepareAsync(
                task,
                Path.Combine(trialDirectory, "agent"),
                $"{SanitizePathPart(task.TaskId)}__{runId}:latest",
                message => logger.Info($"{prefix} status={message}"),
                CancellationToken.None).GetAwaiter().GetResult();
            logger.Info($"{prefix} status=image-ready image={image}");
            string workspaceDirectory = Path.Combine(trialDirectory, "workspace");
            Directory.CreateDirectory(workspaceDirectory);
            logger.Info($"{prefix} status=running-agent");
            if (invocation is null)
            {
                File.WriteAllText(
                    Path.Combine(trialDirectory, "agent", "error.txt"),
                    invocationError ?? "Copilot invocation could not be prepared");
                logger.Info($"{prefix} status=agent-failed error={invocationError}");
            }
            else
            {
                AgentExecutionRequest agentRequest = new(
                    task.TaskId,
                    image,
                    task.RootPath,
                    workspaceDirectory,
                    "bridge",
                    invocation with
                    {
                        Arguments = invocation.Arguments
                            .Concat(["-p", File.ReadAllText(task.InstructionPath)])
                            .ToArray()
                    },
                    [],
                    $"{SanitizePathPart(task.TaskId)}__{runId}");
                try
                {
                    AgentEvidenceSummary agentEvidence = agentExecution.ExecuteAsync(
                        agentRequest,
                        Path.Combine(trialDirectory, "agent"),
                        CancellationToken.None).GetAwaiter().GetResult();
                    File.Copy(
                        Path.Combine(trialDirectory, "agent", agentEvidence.PatchPath),
                        Path.Combine(trialDirectory, "artifacts", "model.patch"),
                        overwrite: true);
                    logger.Info($"{prefix} status=agent-complete result={agentEvidence.Status}");
                }
                catch (AgentExecutionException exception)
                {
                    File.WriteAllText(
                        Path.Combine(trialDirectory, "agent", "error.txt"),
                        exception.Message);
                    logger.Info($"{prefix} status=agent-failed error={exception.Message}");
                }
            }
            logger.Info($"{prefix} status=running-verifier");
            logger.Info($"{prefix} status=verifier-execution-pending");
            logger.Info($"{prefix} status=cleaning-up");
            logger.Info($"{prefix} status=complete result=execution-pending");
            var result = new
            {
                status = "execution-pending",
                taskRoot = resolvedRoot,
                taskId = task.TaskId,
                taskManifest = Path.Combine(task.RootPath, "task.toml"),
                datasetManifest,
                agentImage = image,
                agentWorkspace = workspaceDirectory
            };
            File.WriteAllText(
                Path.Combine(trialDirectory, "result.json"),
                JsonSerializer.Serialize(result, JsonOptions));
        });

        return new RunResult(
            "execution-pending",
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

    private static CopilotInvocation PrepareInvocation(RunConfiguration configuration)
    {
        CopilotAdapter adapter = new(new ProcessCopilotCliProbe());
        return adapter.PrepareAsync(
            new CopilotAdapterOptions(
                configuration.CliVersion,
                configuration.Model,
                configuration.Effort,
                configuration.Effort,
                [],
                new CopilotCredentialSource(
                    CopilotCredentialSourceKind.File,
                    "~/.secrets/copilot_a"),
                [],
                new HashSet<string>(StringComparer.Ordinal)),
            CancellationToken.None).GetAwaiter().GetResult();
    }

    private static string CreateAttemptDirectory(string jobDirectory, string prefix)
    {
        for (int attempt = 1; ; attempt++)
        {
            string name = $"{prefix}-{attempt}";
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
