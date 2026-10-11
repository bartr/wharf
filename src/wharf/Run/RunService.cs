using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
using Tomlyn;
using Tomlyn.Model;
using Wharf.Agent;
using Wharf.Configuration;
using Wharf.Copilot;
using Wharf.Docker;
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
    string OutputDirectory,
    string JobDirectory,
    string AttemptDirectory);

internal sealed record VerifierRetryOutcome(
    TrialResult Result,
    int RetryCount);

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
        string resolvedOutput = Path.GetFullPath(outputDirectory);
        string jobDirectory = Path.Combine(
            resolvedOutput,
            BuildJobDirectoryName(
                configuration.Model,
                configuration.CliVersion,
                invocation?.EffectiveVersion));
        Directory.CreateDirectory(jobDirectory);
        string attemptDirectory = CreateAttemptDirectory(jobDirectory, attemptPrefix);
        logger.AttachJsonlPathIfUnset(
            Path.Combine(jobDirectory, attemptDirectory, "run.jsonl"));
        logger.Info(
            $"plan tasks={tasks.Count} attempts={configuration.K} runs={plannedRuns} " +
            $"task-concurrency={configuration.N} preparation-concurrency={configuration.P} " +
            $"admission-order={(configuration.ShortestFirst ? "shortest-first" : "longest-first")}" +
            (configuration.K > 1 ? $" K={configuration.K}" : ""));
        logger.Info(
            $"run created {attemptDirectory} under {jobDirectory}" +
            (configuration.K > 1 ? $" K={configuration.K}" : ""));

        IReadOnlyList<TaskAttempt> scheduledAttempts = TaskSelector.OrderAttemptsForAdmission(
            TaskSelector.ExpandAttempts(tasks, configuration.K),
            configuration.ShortestFirst);
        using var preparationGate = new PreparationGate(
            configuration.P,
            logger.PreparationGateChanged);
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
            string trialDirectory = Path.Combine(
                jobDirectory,
                attemptDirectory,
                $"{SanitizePathPart(task.TaskId)}__{task.SourceChecksum[..8]}{attemptSuffix}");
            int maximumTrialRetries = configuration.Retries.MaxAttempts ?? 1;
            decimal? totalCost = null;
            for (int trialRetry = 0; ; trialRetry++)
            {
                if (trialRetry > 0)
                {
                    ArchiveTrialExecution(trialDirectory, trialRetry);
                }
                string trialRetrySuffix = trialRetry > 0
                    ? $"__trial-retry-{trialRetry}"
                    : string.Empty;
                string executionName =
                    $"{SanitizePathPart(task.TaskId)}{attemptSuffix}__{runId}{trialRetrySuffix}";
                Directory.CreateDirectory(Path.Combine(trialDirectory, "agent"));
                Directory.CreateDirectory(Path.Combine(trialDirectory, "artifacts"));
                Directory.CreateDirectory(Path.Combine(trialDirectory, "verifier"));
                string workspaceDirectory = Path.Combine(trialDirectory, "workspace");
                Directory.CreateDirectory(workspaceDirectory);
                string image = string.Empty;
                bool removeAgentImage = false;
                AgentExecutionRequest? agentRequest = null;
                string? agentErrorType = null;
                bool agentCompletedSuccessfully = false;
                StageLogContext Stage(string stage) => new(
                    runNumber,
                    plannedRuns,
                    task.TaskId,
                    attempt.AttemptNumber,
                    trialRetry,
                    stage);
                StageLogContext preparationStage = Stage("preparation");
                var preparationDuration = Stopwatch.StartNew();
                bool preparationCompleted = false;
                StageLogContext agentStage = Stage("agent");
                Stopwatch? agentDuration = null;
                bool agentCompleted = false;

                void CompleteExecutionStage(
                    string result,
                    string error,
                    string errorType)
                {
                    if (!preparationCompleted)
                    {
                        preparationDuration.Stop();
                        logger.StageCompleted(
                            preparationStage,
                            preparationDuration.Elapsed,
                            result,
                            error,
                            errorType);
                        preparationCompleted = true;
                    }
                    else if (!agentCompleted && agentDuration is not null)
                    {
                        agentDuration.Stop();
                        logger.StageCompleted(
                            agentStage,
                            agentDuration.Elapsed,
                            result,
                            error,
                            errorType);
                        agentCompleted = true;
                    }
                }

                logger.StageStarted(preparationStage);
                try
                {
                    logger.Info($"{prefix} status=waiting-preparation");
                    agentRequest = await preparationGate.ExecuteAsync(
                        PreparationOperation.Agent,
                        new PreparationGateContext(
                            runNumber,
                            task.TaskId,
                            attempt.AttemptNumber,
                            trialRetry,
                            attempt.AttemptNumber),
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
                                    string requestedImage =
                                        BuildAgentImageName(executionName);
                                    image = await imagePreparer.PrepareAsync(
                                            task,
                                            Path.Combine(trialDirectory, "agent"),
                                            requestedImage,
                                            message => logger.Info($"{prefix} status={message}"),
                                            buildTimeout.Token,
                                            invocation?.EffectiveVersion);
                                    removeAgentImage = string.Equals(
                                        image,
                                        requestedImage,
                                        StringComparison.Ordinal);
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
                                BuildAgentContainerName(executionName),
                                TimeSpan.FromSeconds(task.AgentTimeoutSeconds ?? 5400),
                                RetainWorkspace: configuration.RetainWorkspaces);
                            logger.Info($"{prefix} status=preparing-workspace");
                            await workspacePreparer.PrepareAsync(
                                request,
                                preparationCancellationToken);
                            logger.Info($"{prefix} status=workspace-ready");
                            return request;
                        },
                        trialCancellationToken);
                    preparationDuration.Stop();
                    string preparationResult = agentRequest is null ? "failed" : "completed";
                    logger.StageCompleted(
                        preparationStage,
                        preparationDuration.Elapsed,
                        preparationResult,
                        agentRequest is null ? invocationError : null,
                        agentRequest is null ? "Invocation" : null);
                    preparationCompleted = true;
                    agentDuration = Stopwatch.StartNew();
                    logger.StageStarted(agentStage);

                    if (agentRequest is null)
                    {
                        agentErrorType = "Invocation";
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
                                    ContainerName = BuildAgentContainerName(
                                        $"{executionName}-retry-{retryNumber}")
                                };
                                await preparationGate.ExecuteAsync(
                                    PreparationOperation.AgentRetry,
                                    new PreparationGateContext(
                                        runNumber,
                                        task.TaskId,
                                        attempt.AttemptNumber,
                                        trialRetry,
                                        attempt.AttemptNumber,
                                        retryNumber),
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
                        if (agentOutcome.TotalCost.HasValue)
                        {
                            totalCost = (totalCost ?? 0) + agentOutcome.TotalCost.Value;
                        }
                        File.Copy(
                            Path.Combine(trialDirectory, "agent", agentEvidence.PatchPath),
                            Path.Combine(trialDirectory, "artifacts", "model.patch"),
                            overwrite: true);
                        string cost = totalCost?.ToString(
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
                        if (agentEvidence.Status == "failed")
                        {
                            agentErrorType = agentEvidence.ErrorType ?? "AgentExecution";
                        }
                        else
                        {
                            agentCompletedSuccessfully = true;
                        }
                    }
                    agentDuration.Stop();
                    string agentResult = agentErrorType is null ? "completed" : "failed";
                    logger.StageCompleted(
                        agentStage,
                        agentDuration.Elapsed,
                        agentResult,
                        agentErrorType is null
                            ? null
                            : "agent execution did not complete successfully",
                        agentErrorType);
                    agentCompleted = true;
                }
                catch (OperationCanceledException exception)
                {
                    CompleteExecutionStage(
                        "cancelled",
                        exception.Message,
                        "Cancellation");
                    throw;
                }
                catch (AgentExecutionException exception)
                {
                    agentErrorType = exception.ErrorType;
                    CompleteExecutionStage(
                        "failed",
                        exception.Message,
                        exception.ErrorType);
                    File.WriteAllText(
                        Path.Combine(trialDirectory, "agent", "error.txt"),
                        exception.Message);
                    logger.Info(
                        $"{prefix} status=agent-failed error={exception.Message}",
                        exception.Message,
                        exception.ErrorType);
                }
                catch (AgentImageException exception)
                {
                    agentErrorType = "AgentImage";
                    CompleteExecutionStage("failed", exception.Message, agentErrorType);
                    File.WriteAllText(
                        Path.Combine(trialDirectory, "agent", "error.txt"),
                        exception.Message);
                    logger.Info(
                        $"{prefix} status=agent-failed error={exception.Message}",
                        exception.Message,
                        agentErrorType);
                }
                catch (ImagePullException exception)
                {
                    agentErrorType = "ImagePull";
                    CompleteExecutionStage("failed", exception.Message, agentErrorType);
                    File.WriteAllText(
                        Path.Combine(trialDirectory, "agent", "error.txt"),
                        exception.Message);
                    logger.Info(
                        $"{prefix} status=agent-failed error={exception.Message}",
                        exception.Message,
                        agentErrorType);
                }
                catch (DockerImageException exception)
                {
                    agentErrorType = "DockerImage";
                    CompleteExecutionStage("failed", exception.Message, agentErrorType);
                    File.WriteAllText(
                        Path.Combine(trialDirectory, "agent", "error.txt"),
                        exception.Message);
                    logger.Info(
                        $"{prefix} status=agent-failed error={exception.Message}",
                        exception.Message,
                        agentErrorType);
                }
                string taskStatus = "agent-failed";
                string? patch = null;
                string sourceDirectory = ResolveVerifierSource(task);
                string? verifierError = "agent execution did not produce a patch";
                int verifierRetry = 0;
                string? agentPatchPath = Path.Combine(trialDirectory, "agent", "patch.diff");
                if (File.Exists(agentPatchPath))
                {
                    patch = File.ReadAllText(agentPatchPath);
                }

                if (ShouldRunVerifier(patch))
                {
                    StageLogContext verifierStage = Stage("verifier");
                    var verifierDuration = Stopwatch.StartNew();
                    string verifierStageResult = "failed";
                    string? verifierStageError = null;
                    string? verifierStageErrorType = null;
                    logger.StageStarted(verifierStage);
                    try
                    {
                        string verifierDirectory = Path.Combine(trialDirectory, "verifier");
                        string artifactsDirectory = Path.Combine(trialDirectory, "artifacts");
                        File.Copy(
                            agentPatchPath!,
                            Path.Combine(artifactsDirectory, "model.patch"),
                            overwrite: true);
                        VerifierRetryOutcome verifierOutcome =
                            await ExecuteVerifierWithRetryAsync(
                                async (currentVerifierRetry, verifierCancellationToken) =>
                                {
                                    logger.Info($"{prefix} status=running-verifier");
                                    Directory.CreateDirectory(verifierDirectory);
                                    string? verifierImage = null;
                                    try
                                    {
                                        string verifierRetrySuffix = currentVerifierRetry > 0
                                            ? $"__verifier-retry-{currentVerifierRetry}"
                                            : string.Empty;
                                        (PristineVerifierService Service, PreparedVerifierRun Prepared)
                                            preparedVerifier = await preparationGate.ExecuteAsync(
                                                PreparationOperation.Verifier,
                                                new PreparationGateContext(
                                                    runNumber,
                                                    task.TaskId,
                                                    attempt.AttemptNumber,
                                                    trialRetry,
                                                    attempt.AttemptNumber,
                                                    currentVerifierRetry),
                                                async preparationCancellationToken =>
                                                {
                                                    verifierImage = await PrepareVerifierImageAsync(
                                                        task,
                                                        trialDirectory,
                                                        $"{runId}{attemptSuffix}{trialRetrySuffix}" +
                                                        verifierRetrySuffix,
                                                        logger,
                                                        prefix,
                                                        preparationCancellationToken);
                                                    PristineVerifierService verifierService =
                                                        verifierImage is null
                                                            ? verifier
                                                            : new PristineVerifierService(
                                                                new FilePristineWorkspaceFactory(),
                                                                new RetainingPatchApplier(),
                                                                new DockerVerifierRunner());
                                                    var verifierRequest = new VerifierRequest(
                                                        task.TaskId,
                                                        sourceDirectory,
                                                        workspaceDirectory,
                                                        verifierImage is null ? "bridge" : "none",
                                                        [new VerifierCommand(
                                                            verifierImage is null
                                                                ? "tests"
                                                                : "grader",
                                                            verifierImage is null
                                                                ? VerifierCommandFor(task.Language)
                                                                : "/tests/test.sh",
                                                            TimeSpan.FromSeconds(
                                                                task.VerifierTimeoutSeconds ?? 1800))],
                                                        [],
                                                        [],
                                                        verifierImage,
                                                        verifierDirectory,
                                                        artifactsDirectory);
                                                    PreparedVerifierRun prepared =
                                                        await verifierService.PrepareAsync(
                                                            verifierRequest,
                                                            patch!,
                                                            verifierDirectory,
                                                            preparationCancellationToken);
                                                    return (verifierService, prepared);
                                                },
                                                verifierCancellationToken);
                                        return await preparedVerifier.Service.VerifyPreparedAsync(
                                            preparedVerifier.Prepared,
                                            verifierCancellationToken);
                                    }
                                    finally
                                    {
                                        if (verifierImage is not null)
                                            CleanupVerifierImage(verifierImage, logger, prefix);
                                    }
                                },
                                (nextRetry, exception, _) =>
                                {
                                    logger.Info(
                                        $"{prefix} status=verifier-failed " +
                                        $"error={exception.Message}",
                                        exception.Message,
                                        exception.ErrorType);
                                    ArchiveVerifierExecution(trialDirectory, nextRetry);
                                    verifierRetry = nextRetry;
                                    logger.Info(
                                        $"{prefix} status=verifier-retry retry={nextRetry} " +
                                        $"reason={exception.ErrorType}");
                                    return Task.CompletedTask;
                                },
                                maximumTrialRetries,
                                configuration.Retries.BackoffSeconds,
                                trialCancellationToken);
                        verifierRetry = verifierOutcome.RetryCount;
                        TrialResult verification = verifierOutcome.Result;
                        taskStatus = verification.Status;
                        verifierStageResult = taskStatus;
                        verifierError = null;
                        if (verification.Metrics is { } metrics)
                        {
                            logger.Info(
                                $"{prefix} status=verifier-complete result={taskStatus} " +
                                $"pass@1={metrics.PassAt} f2p={metrics.F2pPercent:0.##}% " +
                                $"p2p={metrics.P2pPercent:0.##}%");
                        }
                        else
                        {
                            logger.Info(
                                $"{prefix} status=verifier-complete result={taskStatus} " +
                                "metrics=unavailable");
                        }
                    }
                    catch (VerifierExecutionException exception)
                    {
                        verifierError = exception.Message;
                        verifierStageError = exception.Message;
                        verifierStageErrorType = exception.ErrorType;
                        logger.Info(
                            $"{prefix} status=verifier-failed error={verifierError}",
                            verifierError,
                            exception.ErrorType);
                    }
                    finally
                    {
                        verifierDuration.Stop();
                        logger.StageCompleted(
                            verifierStage,
                            verifierDuration.Elapsed,
                            trialCancellationToken.IsCancellationRequested
                                ? "cancelled"
                                : verifierStageResult,
                            verifierStageError,
                            verifierStageErrorType);
                    }
                }
                else
                {
                    StageLogContext verifierStage = Stage("verifier");
                    logger.StageStarted(verifierStage);
                    logger.StageCompleted(
                        verifierStage,
                        TimeSpan.Zero,
                        "skipped",
                        verifierError,
                        "AgentExecution");
                    logger.Info(
                        $"{prefix} status=verifier-skipped reason={verifierError}",
                        verifierError,
                        "AgentExecution");
                }
                StageLogContext cleanupStage = Stage("cleanup");
                var cleanupDuration = Stopwatch.StartNew();
                logger.StageStarted(cleanupStage);
                logger.Info($"{prefix} status=cleaning-up");
                string? workspaceCleanupError = null;
                if (!configuration.RetainWorkspaces)
                {
                    try
                    {
                        if (agentRequest is not null)
                        {
                            await PrepareWorkspacesForCleanupAsync(
                                trialDirectory,
                                agentRequest,
                                BuildAgentContainerName(executionName),
                                workspacePreparer,
                                trialCancellationToken);
                        }
                        CleanupWorkspaceDirectories(trialDirectory);
                    }
                    catch (Exception exception) when (
                        exception is AgentExecutionException or
                        IOException or
                        UnauthorizedAccessException)
                    {
                        workspaceCleanupError = exception.Message;
                        logger.Info(
                            $"{prefix} status=workspace-cleanup-failed error={exception.Message}",
                            exception.Message,
                            "WorkspaceCleanup");
                    }
                    catch (OperationCanceledException exception)
                    {
                        cleanupDuration.Stop();
                        logger.StageCompleted(
                            cleanupStage,
                            cleanupDuration.Elapsed,
                            "cancelled",
                            exception.Message,
                            "Cancellation");
                        throw;
                    }
                }
                bool workspaceRetained = HasWorkspaceDirectories(trialDirectory);
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
                    agentWorkspace = Directory.Exists(workspaceDirectory)
                        ? workspaceDirectory
                        : null,
                    workspaceRetained,
                    workspaceCleanupError,
                    trialRetry,
                    verifierRetry,
                    totalCost,
                    verifierError
                };
                File.WriteAllText(
                    Path.Combine(trialDirectory, "result.json"),
                    JsonSerializer.Serialize(result, JsonOptions));
                if (removeAgentImage)
                {
                    try
                    {
                        await imagePreparer.RemoveImageAsync(
                            image,
                            CancellationToken.None);
                        logger.Info(
                            $"{prefix} status=agent-image-cleaned image={image}");
                    }
                    catch (AgentImageException exception)
                    {
                        logger.Info(
                            $"{prefix} status=agent-image-cleanup-failed " +
                            $"error={exception.Message}",
                            exception.Message,
                            "AgentImageCleanup");
                    }
                }
                cleanupDuration.Stop();
                logger.StageCompleted(
                    cleanupStage,
                    cleanupDuration.Elapsed,
                    workspaceCleanupError is null ? "completed" : "failed",
                    workspaceCleanupError,
                    workspaceCleanupError is null ? null : "WorkspaceCleanup");
                if (!ShouldRetryTrial(
                        taskStatus,
                        agentErrorType,
                        agentCompletedSuccessfully,
                        trialRetry,
                        maximumTrialRetries,
                        trialCancellationToken))
                {
                    break;
                }

                int nextRetry = trialRetry + 1;
                logger.Info(
                    $"{prefix} status=trial-retry retry={nextRetry} " +
                    $"reason={agentErrorType}");
                if (configuration.Retries.BackoffSeconds is > 0)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(configuration.Retries.BackoffSeconds.Value),
                        trialCancellationToken);
                }
            }
        }).GetAwaiter().GetResult();

        return new RunResult(
            "completed",
            resolvedRoot,
            datasetManifest,
            tasks.Count,
            resolvedOutput,
            jobDirectory,
            attemptDirectory);
    }

    internal static bool ShouldRetryTrial(
        string taskStatus,
        string? agentErrorType,
        bool agentCompletedSuccessfully,
        int completedRetries,
        int maximumRetries,
        CancellationToken cancellationToken = default) =>
        !cancellationToken.IsCancellationRequested &&
        !agentCompletedSuccessfully &&
        !string.Equals(taskStatus, "passed", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(agentErrorType) &&
        !string.Equals(agentErrorType, "Invocation", StringComparison.Ordinal) &&
        !string.Equals(agentErrorType, "AgentTimeout", StringComparison.Ordinal) &&
        !string.Equals(agentErrorType, "AgentUnfinished", StringComparison.Ordinal) &&
        !IsVerifierError(agentErrorType) &&
        completedRetries < maximumRetries;

    internal static bool ShouldRetryVerifier(
        string? errorType,
        int completedRetries,
        int maximumRetries,
        CancellationToken cancellationToken = default) =>
        !cancellationToken.IsCancellationRequested &&
        IsVerifierError(errorType) &&
        completedRetries < maximumRetries;

    internal static async Task<VerifierRetryOutcome> ExecuteVerifierWithRetryAsync(
        Func<int, CancellationToken, Task<TrialResult>> execute,
        Func<int, VerifierExecutionException, CancellationToken, Task> beforeRetry,
        int maximumRetries,
        int? backoffSeconds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentNullException.ThrowIfNull(beforeRetry);
        if (maximumRetries < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumRetries));
        if (backoffSeconds is < 0)
            throw new ArgumentOutOfRangeException(nameof(backoffSeconds));

        for (int retry = 0; ; retry++)
        {
            try
            {
                return new VerifierRetryOutcome(
                    await execute(retry, cancellationToken),
                    retry);
            }
            catch (VerifierExecutionException exception) when (
                ShouldRetryVerifier(
                    exception.ErrorType,
                    retry,
                    maximumRetries,
                    cancellationToken))
            {
                int nextRetry = retry + 1;
                await beforeRetry(nextRetry, exception, cancellationToken);
                if (backoffSeconds is > 0)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(backoffSeconds.Value),
                        cancellationToken);
                }
            }
        }
    }

    private static bool IsVerifierError(string? errorType) =>
        errorType?.StartsWith("Verifier", StringComparison.Ordinal) == true;

    internal static void ArchiveVerifierExecution(string trialDirectory, int retryNumber)
    {
        string verifierDirectory = Path.Combine(trialDirectory, "verifier");
        if (!Directory.Exists(verifierDirectory))
        {
            throw new DirectoryNotFoundException(
                $"verifier evidence directory does not exist: {verifierDirectory}");
        }

        string retryDirectory = Path.Combine(
            trialDirectory,
            "verifier-retries",
            $"retry-{retryNumber}");
        if (Directory.Exists(retryDirectory))
        {
            throw new IOException(
                $"verifier retry evidence already exists: {retryDirectory}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(retryDirectory)!);
        Directory.Move(verifierDirectory, retryDirectory);
    }

    internal static void ArchiveTrialExecution(string trialDirectory, int retryNumber)
    {
        string retriesDirectory = Path.Combine(trialDirectory, "trial-retries");
        string retryDirectory = Path.Combine(retriesDirectory, $"retry-{retryNumber}");
        Directory.CreateDirectory(retryDirectory);

        foreach (string entry in Directory.EnumerateFileSystemEntries(trialDirectory).ToArray())
        {
            if (string.Equals(entry, retriesDirectory, StringComparison.Ordinal))
            {
                continue;
            }

            string destination = Path.Combine(retryDirectory, Path.GetFileName(entry));
            if (Directory.Exists(entry))
            {
                Directory.Move(entry, destination);
            }
            else
            {
                File.Move(entry, destination);
            }
        }

        string resultPath = Path.Combine(retryDirectory, "result.json");
        string archivedWorkspace = Path.Combine(retryDirectory, "workspace");
        if (Directory.Exists(archivedWorkspace) &&
            File.Exists(resultPath) &&
            JsonNode.Parse(File.ReadAllText(resultPath)) is JsonObject result)
        {
            result["agentWorkspace"] = archivedWorkspace;
            File.WriteAllText(resultPath, result.ToJsonString(JsonOptions));
        }
    }

    internal static void CleanupWorkspaceDirectories(string trialDirectory)
    {
        foreach (string workspace in WorkspaceDirectories(trialDirectory))
        {
            Directory.Delete(workspace, recursive: true);
            string home = workspace + "-home";
            if (Directory.Exists(home))
                Directory.Delete(home, recursive: true);
        }
    }

    private static async Task PrepareWorkspacesForCleanupAsync(
        string trialDirectory,
        AgentExecutionRequest request,
        string executionName,
        IAgentWorkspacePreparer preparer,
        CancellationToken cancellationToken)
    {
        int workspaceNumber = 0;
        foreach (string directory in WorkspaceDirectories(trialDirectory))
        {
            workspaceNumber++;
            await preparer.PrepareForHostCleanupAsync(
                request with
                {
                    WorkspaceDirectory = directory,
                    ContainerName = $"{executionName}-workspace-{workspaceNumber}"
                },
                cancellationToken);
        }
    }

    private static bool HasWorkspaceDirectories(string trialDirectory) =>
        WorkspaceDirectories(trialDirectory).Count > 0;

    private static IReadOnlyList<string> WorkspaceDirectories(string trialDirectory) =>
        Directory.EnumerateDirectories(trialDirectory)
            .Where(directory =>
            {
                string? name = Path.GetFileName(directory);
                return name == "workspace" ||
                    name?.StartsWith(
                        "workspace-infrastructure-retry-",
                        StringComparison.Ordinal) == true &&
                    !name.EndsWith("-home", StringComparison.Ordinal);
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

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

        string image = BuildVerifierImageName(task.TaskId, runId);
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

        DockerRunCleanup.RegisterImage(image);
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

    internal static string BuildJobDirectoryName(
        string model,
        string requestedVersion,
        string? effectiveVersion) =>
        $"{RunConfigurationResolver.DefaultCopilotCli}-{model}-max-" +
        $"{effectiveVersion ?? requestedVersion}";

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

    internal static string BuildAgentImageName(string executionName) =>
        $"agent-{executionName}:latest";

    internal static string BuildAgentContainerName(string executionName) =>
        $"agent-{executionName}";

    internal static string BuildVerifierImageName(string taskId, string runId) =>
        $"verifier-{SanitizePathPart(taskId)}__{runId}:latest";

    internal static IReadOnlyList<string> BuildVerifierImageCleanupArguments(
        IEnumerable<string> images) =>
        [
            "image", "rm",
            .. images
                .Where(image => !string.IsNullOrWhiteSpace(image))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
        ];

    private static void CleanupVerifierImage(
        string image,
        WharfLogger logger,
        string prefix)
    {
        IReadOnlyList<string> arguments =
            BuildVerifierImageCleanupArguments([image]);
        try
        {
            var startInfo = new ProcessStartInfo("docker")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (string argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using Process process = Process.Start(startInfo)
                ?? throw new IOException(
                    "Docker verifier image cleanup process could not be started");
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new IOException(
                    string.IsNullOrWhiteSpace(error)
                        ? output.Trim()
                        : error.Trim());
            }

            logger.Info($"{prefix} status=verifier-image-cleaned image={image}");
            DockerRunCleanup.ForgetImage(image);
        }
        catch (IOException exception)
        {
            logger.Info(
                $"{prefix} status=verifier-image-cleanup-failed error={exception.Message}",
                exception.Message,
                "VerifierImageCleanup");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}

public sealed class RunException(string message) : Exception(message);
