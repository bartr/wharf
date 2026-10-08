using System.Text.Json;
using System.Diagnostics;
using Tomlyn;
using Tomlyn.Model;
using Wharf.Agent;
using Wharf.Configuration;
using Wharf.Copilot;
using Wharf.Execution;
using Wharf.Image;
using Wharf.Logging;
using Wharf.Tasks;
using Wharf.Verifier;

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
    private readonly IAgentWorkspacePreparer workspacePreparer;
    private readonly AgentExecutionService agentExecution;
    private readonly PristineVerifierService verifier;

    public RunService(
        HarborTaskLoader? taskLoader = null,
        IAgentImagePreparer? imagePreparer = null,
        IAgentWorkspacePreparer? workspacePreparer = null,
        AgentExecutionService? agentExecution = null,
        PristineVerifierService? verifier = null)
    {
        this.taskLoader = taskLoader ?? new HarborTaskLoader();
        this.imagePreparer = imagePreparer ?? new DockerAgentImagePreparer();
        this.workspacePreparer = workspacePreparer ?? new DockerAgentWorkspacePreparer();
        this.agentExecution = agentExecution ?? new AgentExecutionService(new DockerAgentRunner());
        this.verifier = verifier ?? new PristineVerifierService(
            new FilePristineWorkspaceFactory(),
            new GitPatchApplier(),
            new ProcessVerifierRunner());
    }

    public RunResult Execute(
        string taskRoot,
        string outputDirectory,
        RunConfiguration configuration,
        WharfLogger logger,
        IReadOnlyList<HarborTask>? selectedTasks = null,
        string attemptPrefix = "all",
        CancellationToken cancellationToken = default)
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
            $"task-concurrency={configuration.N} preparation-concurrency={configuration.B}" +
            (configuration.K > 1 ? $" K={configuration.K}" : ""));
        logger.Info(
            $"run created {attemptDirectory} under {jobDirectory}" +
            (configuration.K > 1 ? $" K={configuration.K}" : ""));

        IReadOnlyList<TaskAttempt> scheduledAttempts = TaskSelector.OrderAttemptsForAdmission(
            TaskSelector.ExpandAttempts(tasks, configuration.K));
        using var preparationGate = new PreparationGate(configuration.B);
        Parallel.ForEachAsync(
            scheduledAttempts.Select((attempt, index) => (attempt, index)),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = configuration.N,
                CancellationToken = cancellationToken
            },
            async (item, trialCancellationToken) =>
        {
            TaskAttempt attempt = item.attempt;
            HarborTask task = attempt.Task;
            int runNumber = item.index + 1;
            string prefix = FormatRunPrefix(
                runNumber,
                plannedRuns,
                task,
                attempt.AttemptNumber,
                configuration.K,
                configuration.K);
            string attemptSuffix = configuration.K > 1
                ? $"__attempt-{attempt.AttemptNumber}"
                : string.Empty;
            string executionName =
                $"{SanitizePathPart(task.TaskId)}{attemptSuffix}__{runId}";
            string trialDirectory = Path.Combine(
                jobDirectory,
                attemptDirectory,
                $"{SanitizePathPart(task.TaskId)}__{task.SourceChecksum[..8]}{attemptSuffix}");
            Directory.CreateDirectory(Path.Combine(trialDirectory, "agent"));
            Directory.CreateDirectory(Path.Combine(trialDirectory, "artifacts"));
            Directory.CreateDirectory(Path.Combine(trialDirectory, "verifier"));
            string workspaceDirectory = Path.Combine(trialDirectory, "workspace");
            Directory.CreateDirectory(workspaceDirectory);
            string image = string.Empty;
            AgentExecutionRequest? agentRequest = null;
            try
            {
                logger.Info($"{prefix} status=waiting-preparation");
                agentRequest = await preparationGate.ExecuteAsync(
                    async preparationCancellationToken =>
                    {
                        logger.Info($"{prefix} status=preparing-image");
                        using (var buildTimeout = CancellationTokenSource.CreateLinkedTokenSource(
                                   preparationCancellationToken))
                        {
                            buildTimeout.CancelAfter(TimeSpan.FromSeconds(
                                task.AgentBuildTimeoutSeconds ?? 1800));
                            try
                            {
                                image = await imagePreparer.PrepareAsync(
                                    task,
                                    Path.Combine(trialDirectory, "agent"),
                                    $"{executionName}:latest",
                                    message => logger.Info($"{prefix} status={message}"),
                                    buildTimeout.Token);
                            }
                            catch (OperationCanceledException) when (
                                !preparationCancellationToken.IsCancellationRequested &&
                                buildTimeout.IsCancellationRequested)
                            {
                                throw new AgentExecutionException(
                                    "agent image build exceeded its timeout",
                                    "AgentBuildTimeout");
                            }
                        }
                        logger.Info($"{prefix} status=image-ready image={image}");
                        if (invocation is null)
                        {
                            return null;
                        }

                        var request = new AgentExecutionRequest(
                            task.TaskId,
                            image,
                            task.RootPath,
                            workspaceDirectory,
                            "bridge",
                            invocation with
                            {
                                Arguments = invocation.Arguments
                                    .Append(BuildPromptArgument(File.ReadAllText(task.InstructionPath)))
                                    .ToArray()
                            },
                            [],
                            executionName,
                            TimeSpan.FromSeconds(task.AgentTimeoutSeconds ?? 5400));
                        logger.Info($"{prefix} status=preparing-workspace");
                        await workspacePreparer.PrepareAsync(
                            request,
                            preparationCancellationToken);
                        logger.Info($"{prefix} status=workspace-ready");
                        return request;
                    },
                    trialCancellationToken);

                if (agentRequest is null)
                {
                    File.WriteAllText(
                        Path.Combine(trialDirectory, "agent", "error.txt"),
                        invocationError ?? "Copilot invocation could not be prepared");
                    logger.Info(
                        $"{prefix} status=agent-failed error={invocationError}",
                        invocationError,
                        "Invocation");
                }
                else
                {
                    logger.Info($"{prefix} status=running-agent");
                    AgentExecutionOutcome agentOutcome =
                        await agentExecution.ExecuteWithInfrastructureRetryAsync(
                        agentRequest,
                        Path.Combine(trialDirectory, "agent"),
                        $"{task.TaskId}__{task.SourceChecksum[..8]}{attemptSuffix}",
                        maximumRetries: 1,
                        async (failedRequest, retryNumber, retryCancellationToken) =>
                        {
                            logger.Info(
                                $"{prefix} status=agent-infrastructure-retry " +
                                $"retry={retryNumber} reason=copilot-service");
                            string retryWorkspace = Path.Combine(
                                trialDirectory,
                                $"workspace-infrastructure-retry-{retryNumber}");
                            Directory.CreateDirectory(retryWorkspace);
                            AgentExecutionRequest retryRequest = failedRequest with
                            {
                                WorkspaceDirectory = retryWorkspace,
                                ContainerName = $"{executionName}-retry-{retryNumber}"
                            };
                            await preparationGate.ExecuteAsync(
                                async prepareCancellationToken =>
                                {
                                    await workspacePreparer.PrepareAsync(
                                        retryRequest,
                                        prepareCancellationToken);
                                    return true;
                                },
                                retryCancellationToken);
                            return retryRequest;
                        },
                        new FileInfrastructureRetrySink(trialDirectory),
                        trialCancellationToken);
                    AgentEvidenceSummary agentEvidence = agentOutcome.Summary;
                    agentRequest = agentOutcome.Request;
                    File.Copy(
                        Path.Combine(trialDirectory, "agent", agentEvidence.PatchPath),
                        Path.Combine(trialDirectory, "artifacts", "model.patch"),
                        overwrite: true);
                    string cost = agentOutcome.TotalCost?.ToString(
                        "0.####",
                        System.Globalization.CultureInfo.InvariantCulture)
                        ?? "unknown";
                    logger.Info(
                        $"{prefix} status=agent-complete result={agentEvidence.Status} cost={cost}",
                        agentEvidence.Status == "failed"
                            ? agentEvidence.Error ?? "agent execution failed"
                            : null,
                        agentEvidence.Status == "failed"
                            ? agentEvidence.ErrorType ?? "AgentExecution"
                            : null);
                }
            }
            catch (AgentExecutionException exception)
            {
                File.WriteAllText(
                    Path.Combine(trialDirectory, "agent", "error.txt"),
                    exception.Message);
                logger.Info(
                    $"{prefix} status=agent-failed error={exception.Message}",
                    exception.Message,
                    exception.ErrorType);
            }
            string taskStatus = "agent-failed";
            string? patch = null;
            string sourceDirectory = ResolveVerifierSource(task);
            string verifierError = "agent execution did not produce a patch";
            string? agentPatchPath = Path.Combine(trialDirectory, "agent", "patch.diff");
            if (File.Exists(agentPatchPath))
            {
                patch = File.ReadAllText(agentPatchPath);
            }

            if (ShouldRunVerifier(patch))
            {
                logger.Info($"{prefix} status=running-verifier");
                try
                {
                    string verifierDirectory = Path.Combine(trialDirectory, "verifier");
                    string artifactsDirectory = Path.Combine(trialDirectory, "artifacts");
                    File.Copy(
                        agentPatchPath!,
                        Path.Combine(artifactsDirectory, "model.patch"),
                        overwrite: true);
                    string? verifierImage = await PrepareVerifierImageAsync(
                        task,
                        trialDirectory,
                        $"{runId}{attemptSuffix}",
                        logger,
                        prefix,
                        trialCancellationToken);
                    PristineVerifierService verifierService = verifierImage is null
                        ? verifier
                        : new PristineVerifierService(
                            new FilePristineWorkspaceFactory(),
                            new RetainingPatchApplier(),
                            new DockerVerifierRunner());
                    TrialResult verification = await verifierService.VerifyAsync(
                        new VerifierRequest(
                            task.TaskId,
                            sourceDirectory,
                            workspaceDirectory,
                            verifierImage is null ? "bridge" : "none",
                            [new VerifierCommand(
                                verifierImage is null ? "tests" : "grader",
                                verifierImage is null ? VerifierCommandFor(task.Language) : "/tests/test.sh",
                                TimeSpan.FromSeconds(task.VerifierTimeoutSeconds ?? 1800))],
                            [],
                            [],
                            verifierImage,
                            verifierDirectory,
                            artifactsDirectory),
                        patch!,
                        verifierDirectory,
                        trialCancellationToken);
                    taskStatus = verification.Status;
                    string? verificationError = taskStatus == "failed"
                        ? "verifier command failed or reported reward 0"
                        : null;
                    if (verification.Metrics is { } metrics)
                    {
                        logger.Info(
                            $"{prefix} status=verifier-complete result={taskStatus} " +
                            $"pass@1={metrics.PassAt} f2p={metrics.F2pPercent:0.##}% " +
                            $"p2p={metrics.P2pPercent:0.##}%",
                            verificationError,
                            verificationError is null ? null : "Verifier");
                    }
                    else
                    {
                        logger.Info(
                            $"{prefix} status=verifier-complete result={taskStatus} " +
                            "metrics=unavailable",
                            verificationError,
                            verificationError is null ? null : "Verifier");
                    }
                }
                catch (VerifierExecutionException exception)
                {
                    verifierError = exception.Message;
                    logger.Info(
                        $"{prefix} status=verifier-failed error={verifierError}",
                        verifierError,
                        exception.ErrorType);
                }
            }
            else
            {
                logger.Info(
                    $"{prefix} status=verifier-skipped reason={verifierError}",
                    verifierError,
                    "AgentExecution");
            }
            logger.Info($"{prefix} status=cleaning-up");
            logger.Info($"{prefix} status=complete result={taskStatus}");
            var result = new
            {
                status = taskStatus,
                taskRoot = resolvedRoot,
                taskId = task.TaskId,
                attempt = attempt.AttemptNumber,
                taskManifest = Path.Combine(task.RootPath, "task.toml"),
                datasetManifest,
                agentImage = image,
                agentWorkspace = workspaceDirectory,
                verifierError
            };
            File.WriteAllText(
                Path.Combine(trialDirectory, "result.json"),
                JsonSerializer.Serialize(result, JsonOptions));
        }).GetAwaiter().GetResult();

        return new RunResult(
            "completed",
            resolvedRoot,
            datasetManifest,
            tasks.Count,
            resolvedOutput);
    }

    private static string ResolveVerifierSource(HarborTask task)
    {
        string appDirectory = Path.Combine(task.RootPath, "environment", "app");
        return Directory.Exists(appDirectory) ? appDirectory : task.RootPath;
    }

    private static string VerifierCommandFor(string language) =>
        language.Trim().ToLowerInvariant() switch
        {
            "go" => "go test ./...",
            "python" or "python3" => "python -m pytest",
            "javascript" or "typescript" or "node" => "npm test",
            _ => "true"
        };

    private static async Task<string?> PrepareVerifierImageAsync(
        HarborTask task,
        string trialDirectory,
        string runId,
        WharfLogger logger,
        string prefix,
        CancellationToken cancellationToken)
    {
        string context = Path.Combine(task.RootPath, "tests");
        string dockerfile = Path.Combine(context, "Dockerfile");
        if (!File.Exists(dockerfile))
        {
            return null;
        }

        string image = $"{SanitizePathPart(task.TaskId)}__verifier__{runId}:latest";
        logger.Info($"{prefix} status=preparing-verifier-image image={image}");
        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string argument in new[] { "image", "build", "--file", dockerfile, "--tag", image, context })
        {
            startInfo.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(startInfo)
            ?? throw new VerifierExecutionException(
                "Docker verifier image process could not be started",
                "VerifierBuild");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(task.VerifierBuildTimeoutSeconds ?? 1800));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested &&
            timeout.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            string timedOutOutput = await outputTask;
            string timedOutError = await errorTask;
            File.WriteAllText(
                Path.Combine(trialDirectory, "verifier", "image-build.log"),
                timedOutOutput + timedOutError);
            throw new VerifierExecutionException(
                "verifier image build exceeded its timeout",
                "VerifierBuildTimeout");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }

        string output = await outputTask;
        string error = await errorTask;
        File.WriteAllText(Path.Combine(trialDirectory, "verifier", "image-build.log"), output + error);
        if (process.ExitCode != 0)
        {
            throw new VerifierExecutionException(
                $"Docker verifier image build failed: " +
                $"{(string.IsNullOrWhiteSpace(error) ? output : error).Trim()}",
                "VerifierBuild");
        }

        logger.Info($"{prefix} status=verifier-image-ready image={image}");
        return image;
    }

    private static string FormatRunPrefix(
        int runNumber,
        int totalRuns,
        HarborTask task,
        int attempt,
        int attempts,
        int k)
    {
        var parts = new List<string>();
        if (totalRuns > 1)
        {
            parts.Add($"run={runNumber:D3}/{totalRuns:D3}");
        }

        parts.Add($"task={ShortTaskName(task.TaskId)}");
        if (attempts > 1)
        {
            parts.Add($"attempt={attempt}/{attempts}");
        }
        if (k > 1)
        {
            parts.Add($"K={k}");
        }

        return string.Join(' ', parts);
    }

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

    internal static string BuildPromptArgument(string prompt) =>
        $"--prompt={prompt}";

    internal static bool ShouldRunVerifier(string? patch) =>
        !string.IsNullOrWhiteSpace(patch);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}

public sealed class RunException(string message) : Exception(message);
