using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Wharf.Agent;
using Wharf.Configuration;
using Wharf.Docker;
using Wharf.Image;
using Wharf.Logging;
using Wharf.Recovery;
using Wharf.State;
using Wharf.Tasks;
using Wharf.Verifier;

namespace Wharf.Run;

internal sealed class BaselineRunService(
    IAgentImagePreparer imagePreparer,
    IBaselineRunner? runner = null)
{
    private readonly IBaselineRunner runner = runner ?? new DockerBaselineRunner();

    internal static string BuildJobDirectoryName(RunConfiguration configuration) =>
        configuration switch
        {
            { NoOp: true, Oracle: true } => "baseline-no-op-oracle",
            { Oracle: true } => "baseline-oracle",
            { NoOp: true } => "baseline-no-op",
            _ => throw new RunException("baseline execution requires --no-op or --oracle")
        };

    public RunResult Execute(
        string taskRoot,
        string outputDirectory,
        RunConfiguration configuration,
        WharfLogger logger,
        IReadOnlyList<HarborTask> tasks,
        string attemptPrefix,
        CancellationToken cancellationToken,
        RunResumeTarget? resume)
    {
        ValidateTasks(tasks, configuration);
        string resolvedOutput = Path.GetFullPath(outputDirectory);
        string jobDirectory = resume is null
            ? Path.Combine(resolvedOutput, BuildJobDirectoryName(configuration))
            : Directory.GetParent(Path.GetFullPath(resume.RunDirectory))!.FullName;
        Directory.CreateDirectory(jobDirectory);
        string attemptDirectory = resume is null
            ? RunService.CreateAttemptDirectory(jobDirectory, attemptPrefix)
            : Path.GetFileName(resume.RunDirectory);
        string runDirectory = Path.Combine(jobDirectory, attemptDirectory);
        if (resume is null)
            RetainedRunMetadataStore.Write(runDirectory, taskRoot, configuration, tasks);
        RetainedRunMetadata retainedPlan = RetainedRunMetadataStore.Read(runDirectory)
            ?? throw new RunException("baseline run plan is missing");
        logger.AttachJsonlPathIfUnset(Path.Combine(runDirectory, "run.jsonl"));
        IReadOnlyList<TaskAttempt> attempts = resume is null
            ? TaskSelector.OrderAttemptsForAdmission(
                TaskSelector.ExpandAttempts(tasks, configuration.K), configuration.ShortestFirst)
            : resume.Attempts.Select(item => item.Attempt).ToArray();
        logger.Info(
            $"{(resume is null ? "plan" : "resume plan")} tasks={tasks.Count} " +
            $"attempts={configuration.K} runs={tasks.Count * configuration.K} " +
            $"task-concurrency={configuration.N} preparation-concurrency={configuration.P} " +
            $"admission-order={(configuration.ShortestFirst ? "shortest-first" : "longest-first")} " +
            $"mode={BuildJobDirectoryName(configuration)}");
        string runId = Guid.NewGuid().ToString("N")[..8];
        using var gate = new PreparationGate(configuration.P, logger.PreparationGateChanged);
        using var allocator = new DockerCpuSetAllocator();
        int? cpuLimit = RunService.ResolveContainerCpuLimit(
            configuration.ContainerCpus, tasks.Count, configuration.K, configuration.N,
            attemptPrefix, resume?.RunDirectory);
        try
        {
            Parallel.ForEachAsync(
                attempts.Select((attempt, index) => (attempt, index)),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = configuration.N,
                    CancellationToken = cancellationToken
                },
                async (item, token) =>
                {
                    TaskAttempt attempt = item.attempt;
                    HarborTask task = attempt.Task;
                    int runNumber = item.index + 1;
                    string prefix =
                        $"run={runNumber:000}/{attempts.Count:000} task={task.TaskId} " +
                        $"attempt={attempt.AttemptNumber}/{configuration.K} K={configuration.K}";
                    string trialDirectory = Path.Combine(
                        runDirectory, RetainedRunResumePlanner.TrialDirectoryName(attempt, configuration.K));
                    if (resume is not null)
                        ResumeEvidenceArchiver.Archive(trialDirectory);
                    Directory.CreateDirectory(trialDirectory);
                    string evidenceDirectory = Path.Combine(trialDirectory, "baseline");
                    Directory.CreateDirectory(evidenceDirectory);
                    string artifactsDirectory = Path.Combine(trialDirectory, "artifacts");
                    Directory.CreateDirectory(artifactsDirectory);
                    string workspace = Path.Combine(trialDirectory, "workspace");
                    Directory.CreateDirectory(workspace);
                    string executionName =
                        $"{task.TaskId}__attempt-{attempt.AttemptNumber}__{runId}";
                    string requestedImage = RunService.BuildAgentImageName(executionName);
                    string? image = null;
                    BaselineRequest? request = null;
                    bool baselineCleaned = false;
                    string status = "completed";
                    string? error = null;
                    string? errorType = null;
                    string? solutionChecksum = retainedPlan.OracleSolutions?.GetValueOrDefault(task.TaskId);
                    TrialResult? verification = null;
                    int verifierRetry = 0;
                    var commands = new List<BaselineCommandResult>();
                    var trialDuration = Stopwatch.StartNew();
                    StageLogContext Stage(string name) =>
                        new(runNumber, attempts.Count, task.TaskId, attempt.AttemptNumber, 0, name);

                    async Task<T> Measure<T>(string stage, Func<Task<T>> action)
                    {
                        var duration = Stopwatch.StartNew();
                        logger.StageStarted(Stage(stage));
                        try
                        {
                            T value = await action();
                            logger.StageCompleted(Stage(stage), duration.Elapsed,
                                value is VerifierRetryOutcome outcome ? outcome.Result.Status : "completed");
                            return value;
                        }
                        catch (Exception exception) when (
                            exception is AgentExecutionException or AgentImageException or
                                ImagePullException or DockerImageException or VerifierExecutionException or
                                RunException or OperationCanceledException or IOException)
                        {
                            logger.StageCompleted(Stage(stage), duration.Elapsed,
                                token.IsCancellationRequested ? "cancelled" : "failed",
                                exception.Message, ErrorTypeFor(exception, token.IsCancellationRequested));
                            throw;
                        }
                    }

                    if (!configuration.Oracle)
                        logger.Info($"{prefix} status=verifier-skipped reason=no-op");
                    try
                    {
                        await Measure("preparation", () => gate.ExecuteAsync(
                            PreparationOperation.Agent,
                            new PreparationGateContext(
                                runNumber, task.TaskId, attempt.AttemptNumber, 0, attempt.AttemptNumber),
                            async preparationToken =>
                            {
                                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(preparationToken);
                                timeout.CancelAfter(TimeSpan.FromSeconds(RunService.ResolveTimeoutSeconds(
                                    configuration.Timeouts.SetupSeconds, task.AgentBuildTimeoutSeconds, 1800)));
                                logger.Info($"{prefix} status=preparing-image");
                                image = await imagePreparer.PrepareAsync(
                                    task, evidenceDirectory, requestedImage,
                                    message => logger.Info($"{prefix} status={message}"), timeout.Token);
                                string? cpuSet = await allocator.AllocateAsync(
                                    image, cpuLimit, item.index, timeout.Token);
                                logger.Info($"{prefix} status=image-ready image={image}");
                                logger.Info($"{prefix} container-cpus={cpuLimit?.ToString() ?? "unrestricted"} " +
                                    $"cpu-set={cpuSet ?? "unrestricted"}");
                                request = new BaselineRequest(
                                    task.TaskId, image, RunService.BuildAgentContainerName(executionName),
                                    evidenceDirectory, artifactsDirectory, workspace,
                                    configuration.Oracle ? Path.Combine(task.RootPath, "solution") : null,
                                    TimeSpan.FromSeconds(RunService.ResolveTimeoutSeconds(
                                        configuration.Timeouts.AgentSeconds, task.AgentTimeoutSeconds, 5400)),
                                    configuration.RetainWorkspaces, cpuSet);
                                await runner.PrepareAsync(request, timeout.Token);
                                return true;
                            }, token));
                        foreach (string step in Steps(configuration))
                        {
                            logger.Info($"{prefix} status=running-{step}");
                            await Measure(step, async () =>
                            {
                                BaselineCommandResult command = await runner.ExecuteAsync(request!, step, token);
                                commands.Add(command);
                                AtomicFile.Replace(
                                    Path.Combine(evidenceDirectory, $"{step}-result.json"),
                                    JsonSerializer.Serialize(command, StateJson.Options));
                                if (command.TimedOut || command.ExitCode != 0)
                                    throw new AgentExecutionException(
                                        $"{step} {(command.TimedOut ? "timed out" : $"exited with code {command.ExitCode}")}",
                                        command.TimedOut ? "BaselineTimeout" : "BaselineCommand");
                                return command;
                            });
                        }
                        if (configuration.Oracle)
                        {
                            await runner.CleanupAsync(request!);
                            baselineCleaned = true;
                            VerifierRetryOutcome outcome = await Measure("verifier", () => VerifyAsync(
                                task, trialDirectory, workspace, artifactsDirectory,
                                configuration, request!.CpuSet, runId, attempt.AttemptNumber,
                                gate, runNumber, logger, prefix, token));
                            verification = outcome.Result;
                            verifierRetry = outcome.RetryCount;
                            status = verification.Status;
                            logger.Info($"{prefix} status=verifier-complete result={status}" +
                                (verification.Metrics is { } metrics
                                    ? $" pass@1={metrics.PassAt} f2p={metrics.F2pPercent:0.##}% p2p={metrics.P2pPercent:0.##}%"
                                    : " metrics=unavailable"));
                        }
                    }
                    catch (Exception exception) when (
                        exception is AgentExecutionException or AgentImageException or ImagePullException or
                            DockerImageException or VerifierExecutionException or RunException or
                            OperationCanceledException or IOException)
                    {
                        status = token.IsCancellationRequested ? "cancelled" : "failed";
                        error = exception.Message;
                        errorType = ErrorTypeFor(exception, token.IsCancellationRequested);
                        logger.Info($"{prefix} status=baseline-failed error={error}", error, errorType);
                    }
                    finally
                    {
                        logger.StageStarted(Stage("cleanup"));
                        logger.Info($"{prefix} status=cleaning-up");
                        var cleanupDuration = Stopwatch.StartNew();
                        string? cleanupError = null;
                        string? cleanupErrorType = null;
                        try
                        {
                            if (request is not null && !baselineCleaned)
                                await runner.CleanupAsync(request);
                        }
                        catch (Exception exception) when (
                            exception is AgentExecutionException or IOException or OperationCanceledException)
                        {
                            status = "failed";
                            error = exception.Message;
                            errorType = "BaselineCleanup";
                            cleanupError = error;
                            cleanupErrorType = errorType;
                            logger.Info($"{prefix} status=baseline-cleanup-failed error={error}", error, errorType);
                        }
                        finally
                        {
                            if (image == requestedImage)
                            {
                                try
                                {
                                    using var imageCleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                                    await imagePreparer.RemoveImageAsync(image, imageCleanupTimeout.Token);
                                }
                                catch (Exception exception) when (
                                    exception is AgentImageException or OperationCanceledException or IOException)
                                {
                                    status = "failed";
                                    error = exception.Message;
                                    errorType = "BaselineImageCleanup";
                                    cleanupError = error;
                                    cleanupErrorType = errorType;
                                    logger.Info($"{prefix} status=baseline-cleanup-failed error={error}", error, errorType);
                                }
                            }
                            try
                            {
                                if (!configuration.RetainWorkspaces && Directory.Exists(workspace))
                                    Directory.Delete(workspace, recursive: true);
                            }
                            catch (IOException exception)
                            {
                                status = "failed";
                                error = cleanupError = exception.Message;
                                errorType = cleanupErrorType = "BaselineWorkspaceCleanup";
                                logger.Info($"{prefix} status=baseline-cleanup-failed error={error}", error, errorType);
                            }
                        }
                        logger.StageCompleted(Stage("cleanup"), cleanupDuration.Elapsed,
                            cleanupErrorType is null ? "completed" : "failed", cleanupError, cleanupErrorType);
                        trialDuration.Stop();
                        var result = new
                        {
                            schemaVersion = "1.0",
                            baselineMode = BuildJobDirectoryName(configuration),
                            status,
                            taskRoot,
                            taskId = task.TaskId,
                            taskChecksum = task.SourceChecksum,
                            attempt = attempt.AttemptNumber,
                            configuration.NoOp,
                            configuration.Oracle,
                            configuration.ContainerCpus,
                            cpuSet = request?.CpuSet,
                            image,
                            totalCost = 0m,
                            agentSeconds = 0,
                            trialRetry = 0,
                            verifierRetry,
                            trialSeconds = trialDuration.Elapsed.TotalSeconds,
                            commands,
                            graded = verification is not null,
                            metrics = verification?.Metrics,
                            error,
                            errorType,
                            solutionChecksum,
                            workspaceRetained = configuration.RetainWorkspaces
                        };
                        AtomicFile.Replace(Path.Combine(trialDirectory, "result.json"),
                            JsonSerializer.Serialize(result, StateJson.Options));
                        logger.Info($"{prefix} status=complete result={status} cost=0");
                    }
                    token.ThrowIfCancellationRequested();
                }).GetAwaiter().GetResult();
        }
        finally
        {
            WriteSummary(runDirectory, configuration, tasks.Count * configuration.K);
        }
        return new RunResult("completed", taskRoot,
            File.Exists(Path.Combine(taskRoot, "dataset.toml")) ? Path.Combine(taskRoot, "dataset.toml") : null,
            tasks.Count, resolvedOutput, jobDirectory, attemptDirectory);
    }

    private static async Task<VerifierRetryOutcome> VerifyAsync(
        HarborTask task, string trialDirectory, string workspace, string artifacts,
        RunConfiguration configuration, string? cpuSet, string runId, int attempt,
        PreparationGate gate, int runNumber, WharfLogger logger, string prefix, CancellationToken token)
    {
        string evidence = Path.Combine(trialDirectory, "verifier");
        Directory.CreateDirectory(evidence);
        string patchPath = Path.Combine(artifacts, "model.patch");
        string patch = File.ReadAllText(patchPath);
        int retainedRetry = RetainedVerifierRetryCount(trialDirectory);
        async Task<TrialResult> Execute(int retry, CancellationToken verifierToken)
        {
            retry += retainedRetry;
            string? image = null;
            try
            {
                (PristineVerifierService Service, PreparedVerifierRun Prepared) prepared =
                    await gate.ExecuteAsync(
                        PreparationOperation.Verifier,
                        new PreparationGateContext(runNumber, task.TaskId, attempt, 0, attempt, retry),
                        async preparationToken =>
                        {
                            image = await RunService.PrepareVerifierImageAsync(
                                task, trialDirectory, $"{runId}__attempt-{attempt}__verifier-retry-{retry}",
                                logger, prefix, RunService.ResolveTimeoutSeconds(
                                    configuration.Timeouts.SetupSeconds, task.VerifierBuildTimeoutSeconds, 1800),
                                preparationToken);
                            var service = new PristineVerifierService(
                                new FilePristineWorkspaceFactory(),
                                image is null ? new GitPatchApplier() : new RetainingPatchApplier(),
                                image is null ? new ProcessVerifierRunner() : new DockerVerifierRunner());
                            var request = new VerifierRequest(
                                task.TaskId, RunService.ResolveVerifierSource(task), workspace,
                                image is null ? "bridge" : "none",
                                [new VerifierCommand(image is null ? "tests" : "grader",
                                image is null ? RunService.VerifierCommandFor(task.Language) : "/tests/test.sh",
                                TimeSpan.FromSeconds(RunService.ResolveTimeoutSeconds(
                                    configuration.Timeouts.VerifierSeconds, task.VerifierTimeoutSeconds, 1800)))],
                                [], [], image, evidence, artifacts, cpuSet, AllowEmptyPatch: true);
                            return (service, await service.PrepareAsync(request, patch, evidence, preparationToken));
                        }, verifierToken);
                return await prepared.Service.VerifyPreparedAsync(prepared.Prepared, verifierToken);
            }
            finally
            {
                if (image is not null)
                    RunService.CleanupVerifierImage(image, logger, prefix);
            }
        }
        VerifierRetryOutcome outcome = await RunService.ExecuteVerifierWithRetryAsync(
            Execute,
            (retry, exception, _) =>
            {
                int nextRetry = retainedRetry + retry;
                RunService.ArchiveVerifierExecution(trialDirectory, nextRetry);
                logger.Info($"{prefix} status=verifier-retry retry={nextRetry} reason={exception.ErrorType}");
                return Task.CompletedTask;
            },
            Math.Max(0, (configuration.Retries.MaxAttempts ?? 1) - retainedRetry),
            configuration.Retries.BackoffSeconds, token);
        return outcome with { RetryCount = retainedRetry + outcome.RetryCount };
    }

    internal static int RetainedVerifierRetryCount(string trialDirectory)
    {
        string directory = Path.Combine(trialDirectory, "verifier-retries");
        if (!Directory.Exists(directory))
            return 0;
        var retries = new HashSet<int>();
        foreach (string path in Directory.EnumerateDirectories(directory))
        {
            string name = Path.GetFileName(path);
            if (!name.StartsWith("retry-", StringComparison.Ordinal) ||
                !int.TryParse(name.AsSpan("retry-".Length), out int retry) ||
                retry < 1 || !retries.Add(retry))
            {
                throw new RunException($"invalid retained verifier retry identity: {path}");
            }
        }
        if (retries.Count > 0 && retries.Max() != retries.Count)
            throw new RunException("retained verifier retry identities must be contiguous");
        return retries.Count;
    }

    private static IEnumerable<string> Steps(RunConfiguration configuration)
    {
        if (configuration.NoOp)
            yield return "no-op";
        if (configuration.Oracle)
            yield return "oracle";
    }

    private static string ErrorTypeFor(Exception exception, bool cancelled) =>
        exception switch
        {
            AgentExecutionException agent => agent.ErrorType,
            VerifierExecutionException verifier => verifier.ErrorType,
            OperationCanceledException => cancelled ? "Cancellation" : "BaselinePreparationTimeout",
            _ => exception.GetType().Name
        };

    private static void ValidateTasks(IReadOnlyList<HarborTask> tasks, RunConfiguration configuration)
    {
        if (!configuration.Oracle)
            return;
        foreach (HarborTask task in tasks)
        {
            string solution = Path.Combine(task.RootPath, "solution", "solve.sh");
            if (!File.Exists(solution))
                throw new RunException($"oracle solution does not exist: {solution}");
        }
    }

    internal static string SolutionChecksum(string directory)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                     .OrderBy(path => Path.GetRelativePath(directory, path), StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(directory, path).Replace('\\', '/') + "\0"));
            hash.AppendData(File.ReadAllBytes(path));
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void WriteSummary(string runDirectory, RunConfiguration configuration, int planned)
    {
        var trials = Directory.EnumerateDirectories(runDirectory)
            .Select(directory => Path.Combine(directory, "result.json"))
            .Where(File.Exists)
            .Select(path => JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(path)))
            .ToArray();
        DateTimeOffset[] stageTimes = File.ReadLines(Path.Combine(runDirectory, "run.jsonl"))
            .Select(line => JsonSerializer.Deserialize<JsonElement>(line))
            .Where(record => record.TryGetProperty("eventType", out JsonElement type) &&
                type.GetString() == "stage")
            .Select(record => record.GetProperty("timestamp").GetDateTimeOffset())
            .ToArray();
        AtomicFile.Replace(Path.Combine(runDirectory, "baseline-summary.json"),
            JsonSerializer.Serialize(new
            {
                schemaVersion = "1.0",
                baselineMode = BuildJobDirectoryName(configuration),
                plannedTrials = planned,
                terminalTrials = trials.Length,
                elapsedSeconds = stageTimes.Length == 0
                    ? (double?)null : (stageTimes.Max() - stageTimes.Min()).TotalSeconds,
                meanTrialSeconds = trials.Length == 0
                    ? (double?)null : trials.Average(t => t.GetProperty("trialSeconds").GetDouble()),
                gradedTrials = trials.Count(t => t.GetProperty("graded").GetBoolean()),
                passAt1 = configuration.Oracle
                    ? trials.Count(t => t.GetProperty("status").GetString() == "passed") / (double)planned
                    : (double?)null,
                passingTrials = trials.Count(t => t.GetProperty("status").GetString() == "passed"),
                failedTrials = trials.Count(t => t.GetProperty("status").GetString() == "failed"),
                cancelledTrials = trials.Count(t => t.GetProperty("status").GetString() == "cancelled"),
                totalCost = 0m,
                agentSeconds = 0,
                trials
            }, StateJson.Options));
    }
}
