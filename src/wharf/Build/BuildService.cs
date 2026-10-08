using Wharf.Image;
using Wharf.Logging;
using Wharf.Tasks;

namespace Wharf.Build;

public sealed record BuildResult(
    string TaskRoot,
    int TaskCount,
    int PreparedCount,
    int BuiltCount);

public sealed class BuildService
{
    private readonly HarborTaskLoader taskLoader;
    private readonly IAgentImagePreparer imagePreparer;

    public BuildService(
        HarborTaskLoader? taskLoader = null,
        IAgentImagePreparer? imagePreparer = null)
    {
        this.taskLoader = taskLoader ?? new HarborTaskLoader();
        this.imagePreparer = imagePreparer ?? new DockerAgentImagePreparer();
    }

    public BuildResult Execute(
        string taskRoot,
        WharfLogger logger)
    {
        string resolvedRoot = Path.GetFullPath(taskRoot);
        if (!Directory.Exists(resolvedRoot))
            throw new BuildException($"build task directory does not exist: {resolvedRoot}");

        IReadOnlyList<HarborTask> tasks = taskLoader.Load(resolvedRoot);
        if (tasks.Count == 0)
            throw new BuildException($"build task directory contains no task.toml files: {resolvedRoot}");

        string evidenceRoot = Path.Combine(
            Path.GetTempPath(),
            $"wharf-build-{Guid.NewGuid():N}");
        Directory.CreateDirectory(evidenceRoot);
        int preparedCount = 0;
        int builtCount = 0;
        try
        {
            foreach (HarborTask task in tasks)
            {
                bool hasExistingImage = !string.IsNullOrWhiteSpace(task.EnvironmentImage);
                string image = hasExistingImage
                    ? task.EnvironmentImage!
                    : $"{Sanitize(task.TaskId)}__{task.SourceChecksum[..12]}:latest";
                string evidenceDirectory = Path.Combine(evidenceRoot, Sanitize(task.TaskId));
                logger.Info($"build task={task.TaskId} status=preparing-image image={image}");
                string builtImage = imagePreparer.PrepareAsync(
                    task,
                    evidenceDirectory,
                    image,
                    message => logger.Info($"build task={task.TaskId} status={message}"),
                    CancellationToken.None).GetAwaiter().GetResult();
                preparedCount++;
                if (hasExistingImage)
                {
                    logger.Info($"build task={task.TaskId} status=image-ready image={builtImage}");
                }
                else
                {
                    imagePreparer.RemoveImageAsync(
                        builtImage,
                        CancellationToken.None).GetAwaiter().GetResult();
                    logger.Info($"build task={task.TaskId} status=image-removed image={builtImage}");
                    builtCount++;
                }
            }
        }
        finally
        {
            if (Directory.Exists(evidenceRoot))
                Directory.Delete(evidenceRoot, recursive: true);
        }

        return new BuildResult(resolvedRoot, tasks.Count, preparedCount, builtCount);
    }

    private static string Sanitize(string value)
    {
        var invalidCharacters = new HashSet<char>(Path.GetInvalidFileNameChars());
        return new string(value.Select(character =>
            invalidCharacters.Contains(character) ? '_' : character).ToArray());
    }
}

public sealed class BuildException(string message) : Exception(message);
