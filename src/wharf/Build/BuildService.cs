using Wharf.Image;
using Wharf.Logging;
using Wharf.Tasks;
using Wharf.Copilot;
using Wharf.Run;
using Wharf.Verifier;

namespace Wharf.Build;

public sealed record BuildResult(
    string TaskRoot,
    int TaskCount,
    int PreparedCount,
    int BuiltCount,
    int VerifierPreparedCount = 0,
    string? CliVersion = null,
    int PreparationConcurrency = 4);

public sealed class BuildService
{
    private readonly HarborTaskLoader taskLoader;
    private readonly IAgentImagePreparer imagePreparer;
    private readonly CopilotAdapter copilot;
    private readonly Func<HarborTask, string, string, WharfLogger, CancellationToken, Task<string?>> prepareVerifier;

    public BuildService(
        HarborTaskLoader? taskLoader = null,
        IAgentImagePreparer? imagePreparer = null,
        ICopilotCliProbe? cliProbe = null,
        Func<HarborTask, string, string, WharfLogger, CancellationToken, Task<string?>>? prepareVerifier = null)
    {
        this.taskLoader = taskLoader ?? new HarborTaskLoader();
        this.imagePreparer = imagePreparer ?? new DockerAgentImagePreparer();
        copilot = new CopilotAdapter(cliProbe ?? new NpmRegistryCopilotCliProbe());
        this.prepareVerifier = prepareVerifier ?? PrepareVerifierAsync;
    }

    public BuildResult Execute(
        string taskRoot,
        WharfLogger logger,
        int preparationConcurrency = 4,
        string requestedCliVersion = "latest",
        CancellationToken cancellationToken = default)
    {
        if (preparationConcurrency < 1)
            throw new BuildException("P must be positive");
        string resolvedRoot = Path.GetFullPath(taskRoot);
        if (!Directory.Exists(resolvedRoot))
            throw new BuildException($"build task directory does not exist: {resolvedRoot}");

        IReadOnlyList<HarborTask> tasks = taskLoader.Load(resolvedRoot);
        if (tasks.Count == 0)
            throw new BuildException($"build task directory contains no task.toml files: {resolvedRoot}");

        string cliVersion = copilot.ResolveVersionAsync(
            requestedCliVersion, cancellationToken).GetAwaiter().GetResult();
        cancellationToken.ThrowIfCancellationRequested();
        string evidenceRoot = Path.Combine(
            Path.GetTempPath(),
            $"wharf-build-{Guid.NewGuid():N}");
        Directory.CreateDirectory(evidenceRoot);
        int preparedCount = 0;
        int builtCount = 0;
        int verifierPreparedCount = 0;
        try
        {
            logger.Info($"build tasks={tasks.Count} preparation-concurrency={preparationConcurrency} cli-version={cliVersion}");
            Parallel.ForEachAsync(
                tasks,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = preparationConcurrency,
                    CancellationToken = cancellationToken
                },
                async (task, token) =>
                {
                    string identity = $"build-{Guid.NewGuid():N}";
                    string image = RunService.BuildAgentImageName($"{task.TaskId}__{identity}");
                    string evidenceDirectory = Path.Combine(evidenceRoot, identity);
                    string? agentImage = null;
                    string? verifierImage = null;
                    Exception? taskError = null;
                    try
                    {
                        logger.Info($"build task={task.TaskId} status=preparing-image image={image}");
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                        timeout.CancelAfter(TimeSpan.FromSeconds(
                            RunService.ResolveTimeoutSeconds(null, task.AgentBuildTimeoutSeconds, 1800)));
                        agentImage = await imagePreparer.PrepareAsync(
                            task, Path.Combine(evidenceDirectory, "agent"), image,
                            message => logger.Info($"build task={task.TaskId} status={message}"),
                            timeout.Token, cliVersion);
                        if (!string.Equals(agentImage, image, StringComparison.Ordinal))
                        {
                            agentImage = null;
                            throw new BuildException($"build task {task.TaskId} returned an unexpected child image");
                        }
                        Directory.CreateDirectory(Path.Combine(evidenceDirectory, "verifier"));
                        verifierImage = await prepareVerifier(
                            task, evidenceDirectory, identity, logger, token);
                        if (verifierImage is not null && !string.Equals(
                                verifierImage, RunService.BuildVerifierImageName(task.TaskId, identity),
                                StringComparison.Ordinal))
                        {
                            verifierImage = null;
                            throw new BuildException($"build task {task.TaskId} returned an unexpected verifier child image");
                        }
                        if (verifierImage is not null)
                            Interlocked.Increment(ref verifierPreparedCount);
                        Interlocked.Increment(ref preparedCount);
                    }
                    catch (Exception exception) when (
                        exception is AgentImageException or VerifierExecutionException or
                            BuildException or IOException or OperationCanceledException)
                    {
                        taskError = exception;
                        logger.Info($"build task={task.TaskId} status=failed error={exception.Message}",
                            exception.Message, "BuildPreparation");
                        throw;
                    }
                    finally
                    {
                        var cleanupErrors = new List<string>();
                        foreach (string? child in new[] { verifierImage, agentImage })
                        {
                            if (child is null)
                                continue;
                            try
                            {
                                using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                                await imagePreparer.RemoveImageAsync(child, cleanupTimeout.Token);
                                logger.Info($"build task={task.TaskId} status=image-removed image={child}");
                            }
                            catch (Exception exception) when (
                                exception is AgentImageException or IOException or OperationCanceledException)
                            {
                                cleanupErrors.Add($"{child}: {exception.Message}");
                                logger.Info($"build task={task.TaskId} status=cleanup-failed image={child} error={exception.Message}",
                                    exception.Message, "BuildCleanup");
                            }
                        }
                        if (cleanupErrors.Count > 0)
                            throw new BuildException(
                                $"build cleanup failed: {string.Join("; ", cleanupErrors)}" +
                                (taskError is null ? "" : $"; preparation error: {taskError.Message}"));
                    }
                    Interlocked.Increment(ref builtCount);
                }).GetAwaiter().GetResult();
        }
        finally
        {
            if (Directory.Exists(evidenceRoot))
                Directory.Delete(evidenceRoot, recursive: true);
        }

        return new BuildResult(
            resolvedRoot, tasks.Count, preparedCount, builtCount,
            verifierPreparedCount, cliVersion, preparationConcurrency);
    }

    private static Task<string?> PrepareVerifierAsync(
        HarborTask task, string directory, string identity,
        WharfLogger logger, CancellationToken cancellationToken) =>
        RunService.PrepareVerifierImageAsync(
            task, directory, identity, logger, $"build task={task.TaskId}",
            RunService.ResolveTimeoutSeconds(null, task.VerifierBuildTimeoutSeconds, 1800),
            cancellationToken);
}

public sealed class BuildException(string message) : Exception(message);
