using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Wharf.Docker;
using Wharf.Tasks;

namespace Wharf.Image;

public interface IAgentImagePreparer
{
    Task<string> PrepareAsync(
        HarborTask task,
        string evidenceDirectory,
        string imageName,
        Action<string>? progress = null,
        CancellationToken cancellationToken = default,
        string? copilotVersion = null);

    Task RemoveImageAsync(
        string image,
        CancellationToken cancellationToken = default);
}

public sealed class DockerAgentImagePreparer : IAgentImagePreparer
{
    private const string CacheSchemaVersion = "1";
    private const string CopilotInstallPath = "/opt/wharf/copilot";
    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> cacheBuilds =
        new(StringComparer.Ordinal);
    private readonly IImageClient images;
    private readonly ImagePullCoordinator imagePulls;
    private readonly Action<string>? progress;

    public DockerAgentImagePreparer(
        ImagePullCoordinator? imagePulls = null,
        IImageClient? images = null,
        Action<string>? progress = null)
    {
        this.images = images ?? new DockerImageClient();
        this.imagePulls = imagePulls ?? new ImagePullCoordinator(
            this.images,
            new SystemAsyncDelay(),
            new SystemRandomSource(),
            new NullImagePullTelemetry(),
            new ImagePullConsole(0));
        this.progress = progress;
    }

    public async Task<string> PrepareAsync(
        HarborTask task,
        string evidenceDirectory,
        string imageName,
        Action<string>? progress = null,
        CancellationToken cancellationToken = default,
        string? copilotVersion = null)
    {
        progress ??= this.progress;
        Directory.CreateDirectory(evidenceDirectory);
        if (string.IsNullOrWhiteSpace(imageName))
            throw new AgentImageException("agent image name is required");
        string environmentImage;
        if (!string.IsNullOrWhiteSpace(task.EnvironmentImage))
        {
            progress?.Invoke($"agent image checking {task.EnvironmentImage}");
            ImagePullResult pullResult = await imagePulls.PrepareAsync(
                task.EnvironmentImage,
                cancellationToken);
            progress?.Invoke(
                $"agent image {pullResult.Outcome}: {task.EnvironmentImage}");
            File.WriteAllText(
                Path.Combine(evidenceDirectory, "image-pull.json"),
                System.Text.Json.JsonSerializer.Serialize(pullResult));
            environmentImage = task.EnvironmentImage;
        }
        else
        {
            environmentImage = await PrepareTaskEnvironmentAsync(
                task,
                evidenceDirectory,
                imageName,
                progress,
                cancellationToken,
                persistent: copilotVersion is not null);
            if (copilotVersion is null)
                return environmentImage;
        }

        string trialBaseImage = environmentImage;
        if (copilotVersion is not null)
        {
            ValidateCopilotVersion(copilotVersion);
            CachedImageResult cli = await PrepareCopilotPayloadAsync(
                environmentImage,
                copilotVersion,
                evidenceDirectory,
                progress,
                cancellationToken);
            CachedImageResult agent = await PrepareAgentCacheAsync(
                task,
                environmentImage,
                cli.Image,
                copilotVersion,
                evidenceDirectory,
                progress,
                cancellationToken);
            trialBaseImage = agent.Image;
            WriteCacheEvidence(
                evidenceDirectory,
                environmentImage,
                copilotVersion,
                cli,
                agent);
        }

        return await BuildTrialImageAsync(
            trialBaseImage,
            imageName,
            evidenceDirectory,
            progress,
            cancellationToken);
    }

    private async Task<string> PrepareTaskEnvironmentAsync(
        HarborTask task,
        string evidenceDirectory,
        string imageName,
        Action<string>? progress,
        CancellationToken cancellationToken,
        bool persistent)
    {
        string context = Path.Combine(task.RootPath, "environment");
        string dockerfile = Path.Combine(context, "Dockerfile");
        if (!File.Exists(dockerfile))
            throw new AgentImageException($"agent Dockerfile does not exist: {dockerfile}");

        string baseImage = ReadBaseImage(dockerfile);
        progress?.Invoke($"agent image preparing task {task.TaskId}");
        progress?.Invoke($"agent image checking base {baseImage}");
        ImagePullResult baseResult = await imagePulls.PrepareAsync(baseImage, cancellationToken);
        progress?.Invoke($"agent image base {baseResult.Outcome}: {baseImage}");

        string buildDockerfile = Path.Combine(
            evidenceDirectory,
            persistent ? "Dockerfile.task-cache" : "Dockerfile.wharf");
        string[] configuredDockerfile = File.ReadAllLines(dockerfile);
        File.WriteAllLines(buildDockerfile, configuredDockerfile);
        string image = imageName;
        if (persistent)
        {
            string baseId = await InspectImageIdAsync(baseImage, cancellationToken);
            string key = CreateCacheKey(
                CacheSchemaVersion,
                task.SourceChecksum,
                baseId,
                string.Join('\n', configuredDockerfile));
            image = $"wharf-task-env:{SanitizeTag(task.TaskId)}-{key[..12]}";
            CachedImageResult cached = await PreparePersistentImageAsync(
                image,
                async buildCancellationToken =>
                {
                    progress?.Invoke($"agent task environment building {image}");
                    ProcessResult result = await RunDockerAsync(
                        [
                            "image", "build",
                            "--file", buildDockerfile,
                            "--tag", image,
                            "--label", "com.bartr.wharf.cache=true",
                            "--label", "com.bartr.wharf.cache-kind=task-environment",
                            "--label", $"com.bartr.wharf.cache-schema={CacheSchemaVersion}",
                            context
                        ],
                        buildCancellationToken);
                    File.WriteAllText(
                        Path.Combine(evidenceDirectory, "task-image-build.log"),
                        result.StandardOutput + result.StandardError);
                },
                cancellationToken);
            progress?.Invoke(
                $"agent task environment {(cached.Reused ? "cache-hit" : "built")} {image}");
            return image;
        }

        progress?.Invoke($"agent image building {image}");
        ProcessResult build = await RunDockerAsync(
            ["image", "build", "--file", buildDockerfile, "--tag", image, context],
            cancellationToken);
        File.WriteAllText(
            Path.Combine(evidenceDirectory, "image-build.log"),
            build.StandardOutput + build.StandardError);
        DockerRunCleanup.RegisterImage(image);
        progress?.Invoke($"agent image built {image}");
        return image;
    }

    private async Task<CachedImageResult> PrepareCopilotPayloadAsync(
        string installerImage,
        string copilotVersion,
        string evidenceDirectory,
        Action<string>? progress,
        CancellationToken cancellationToken)
    {
        string image = BuildCopilotCacheImageName(copilotVersion);
        string dockerfile = Path.Combine(evidenceDirectory, "Dockerfile.copilot-cache");
        File.WriteAllText(
            dockerfile,
            BuildCopilotPayloadDockerfile(installerImage, copilotVersion));
        CachedImageResult cached = await PreparePersistentImageAsync(
            image,
            async buildCancellationToken =>
            {
                progress?.Invoke($"agent Copilot CLI cache building {image}");
                ProcessResult result = await RunDockerAsync(
                    ["image", "build", "--file", dockerfile, "--tag", image, evidenceDirectory],
                    buildCancellationToken);
                File.WriteAllText(
                    Path.Combine(evidenceDirectory, "copilot-image-build.log"),
                    result.StandardOutput + result.StandardError);
            },
            cancellationToken);
        progress?.Invoke($"agent Copilot CLI {(cached.Reused ? "cache-hit" : "built")} {image}");
        return cached;
    }

    private async Task<CachedImageResult> PrepareAgentCacheAsync(
        HarborTask task,
        string environmentImage,
        string cliImage,
        string copilotVersion,
        string evidenceDirectory,
        Action<string>? progress,
        CancellationToken cancellationToken)
    {
        string environmentId = await InspectImageIdAsync(environmentImage, cancellationToken);
        string cliId = await InspectImageIdAsync(cliImage, cancellationToken);
        string key = CreateCacheKey(
            CacheSchemaVersion,
            environmentId,
            cliId,
            copilotVersion);
        string image = $"wharf-agent-cache:{SanitizeTag(task.TaskId)}-{key[..12]}";
        string dockerfile = Path.Combine(evidenceDirectory, "Dockerfile.agent-cache");
        File.WriteAllText(
            dockerfile,
            BuildAgentCacheDockerfile(
                environmentImage,
                cliImage,
                copilotVersion,
                key));
        CachedImageResult cached = await PreparePersistentImageAsync(
            image,
            async buildCancellationToken =>
            {
                progress?.Invoke($"agent image cache building {image}");
                ProcessResult result = await RunDockerAsync(
                    ["image", "build", "--file", dockerfile, "--tag", image, evidenceDirectory],
                    buildCancellationToken);
                File.WriteAllText(
                    Path.Combine(evidenceDirectory, "agent-cache-build.log"),
                    result.StandardOutput + result.StandardError);
            },
            cancellationToken);
        progress?.Invoke($"agent image {(cached.Reused ? "cache-hit" : "built")} {image}");
        return cached;
    }

    private async Task<CachedImageResult> PreparePersistentImageAsync(
        string image,
        Func<CancellationToken, Task> build,
        CancellationToken cancellationToken)
    {
        if (await images.ExistsAsync(image, cancellationToken))
            return new CachedImageResult(image, true);

        Lazy<Task<string>> pending = cacheBuilds.GetOrAdd(
            image,
            cacheImage => new Lazy<Task<string>>(
                async () =>
                {
                    try
                    {
                        if (!await images.ExistsAsync(cacheImage, cancellationToken))
                            await build(cancellationToken);
                        return cacheImage;
                    }
                    finally
                    {
                        cacheBuilds.TryRemove(cacheImage, out _);
                    }
                },
                LazyThreadSafetyMode.ExecutionAndPublication));
        await pending.Value.WaitAsync(cancellationToken);
        if (!await images.ExistsAsync(image, cancellationToken))
            throw new AgentImageException($"cached Docker image was not created: {image}");
        return new CachedImageResult(image, false);
    }

    private static async Task<string> BuildTrialImageAsync(
        string baseImage,
        string imageName,
        string evidenceDirectory,
        Action<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Invoke($"agent image building isolated trial image {imageName}");
        string dockerfile = Path.Combine(evidenceDirectory, "Dockerfile.wharf");
        File.WriteAllText(
            dockerfile,
            BuildIsolatedImageDockerfile(baseImage, imageName));
        ProcessResult build = await RunDockerAsync(
            BuildIsolatedImageArguments(dockerfile, imageName, evidenceDirectory),
            cancellationToken);
        File.WriteAllText(
            Path.Combine(evidenceDirectory, "image-build.log"),
            build.StandardOutput + build.StandardError);
        DockerRunCleanup.RegisterImage(imageName);
        progress?.Invoke($"agent image built {imageName}");
        return imageName;
    }

    internal static string BuildCopilotPayloadDockerfile(
        string installerImage,
        string copilotVersion)
    {
        if (string.IsNullOrWhiteSpace(installerImage))
            throw new AgentImageException("Copilot installer image is required");
        ValidateCopilotVersion(copilotVersion);
        return
            $"# syntax=docker/dockerfile:1{Environment.NewLine}" +
            $"FROM {installerImage} AS installer{Environment.NewLine}" +
            $"RUN mkdir -p {CopilotInstallPath} \\\n" +
            $" && npm install --prefix {CopilotInstallPath} --no-fund --no-audit " +
            $"@github/copilot@{copilotVersion} \\\n" +
            $" && node -e \"if (require('{CopilotInstallPath}/node_modules/@github/copilot/package.json')" +
            $".version !== '{copilotVersion}') process.exit(1)\"{Environment.NewLine}" +
            $"FROM scratch{Environment.NewLine}" +
            $"COPY --link --from=installer {CopilotInstallPath} {CopilotInstallPath}{Environment.NewLine}" +
            $"LABEL com.bartr.wharf.cache=\"true\"{Environment.NewLine}" +
            $"LABEL com.bartr.wharf.cache-kind=\"copilot-cli\"{Environment.NewLine}" +
            $"LABEL com.bartr.wharf.cache-schema=\"{CacheSchemaVersion}\"{Environment.NewLine}" +
            $"LABEL com.bartr.wharf.cli-version=\"{copilotVersion}\"{Environment.NewLine}";
    }

    internal static string BuildAgentCacheDockerfile(
        string environmentImage,
        string cliImage,
        string copilotVersion,
        string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(environmentImage))
            throw new AgentImageException("agent environment image is required");
        if (string.IsNullOrWhiteSpace(cliImage))
            throw new AgentImageException("Copilot CLI image is required");
        if (string.IsNullOrWhiteSpace(cacheKey))
            throw new AgentImageException("agent cache key is required");
        ValidateCopilotVersion(copilotVersion);
        return
            $"# syntax=docker/dockerfile:1{Environment.NewLine}" +
            $"FROM {cliImage} AS cli{Environment.NewLine}" +
            $"FROM {environmentImage}{Environment.NewLine}" +
            $"COPY --link --from=cli {CopilotInstallPath} {CopilotInstallPath}{Environment.NewLine}" +
            $"ENV PATH=\"{CopilotInstallPath}/node_modules/.bin:${{PATH}}\"{Environment.NewLine}" +
            $"LABEL com.bartr.wharf.cache=\"true\"{Environment.NewLine}" +
            $"LABEL com.bartr.wharf.cache-kind=\"agent\"{Environment.NewLine}" +
            $"LABEL com.bartr.wharf.cache-schema=\"{CacheSchemaVersion}\"{Environment.NewLine}" +
            $"LABEL com.bartr.wharf.cache-key=\"{cacheKey}\"{Environment.NewLine}" +
            $"LABEL com.bartr.wharf.cli-version=\"{copilotVersion}\"{Environment.NewLine}";
    }

    internal static string BuildCopilotCacheImageName(string copilotVersion)
    {
        ValidateCopilotVersion(copilotVersion);
        string architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "amd64",
            Architecture.Arm64 => "arm64",
            _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()
        };
        return
            $"wharf-copilot-cli:{SanitizeTag(copilotVersion)}-linux-" +
            $"{architecture}-v{CacheSchemaVersion}";
    }

    private static void WriteCacheEvidence(
        string evidenceDirectory,
        string environmentImage,
        string copilotVersion,
        CachedImageResult cli,
        CachedImageResult agent)
    {
        var evidence = new
        {
            schemaVersion = CacheSchemaVersion,
            environmentImage,
            copilotVersion,
            cliImage = cli.Image,
            cliOutcome = cli.Reused ? "cache-hit" : "built-or-coalesced",
            agentImage = agent.Image,
            agentOutcome = agent.Reused ? "cache-hit" : "built-or-coalesced"
        };
        File.WriteAllText(
            Path.Combine(evidenceDirectory, "agent-image-cache.json"),
            System.Text.Json.JsonSerializer.Serialize(
                evidence,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private static string CreateCacheKey(params string[] values)
    {
        var canonical = new StringBuilder();
        foreach (string value in values)
            canonical.Append(value.Length).Append(':').Append(value);
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static string SanitizeTag(string value)
    {
        var sanitized = new StringBuilder(value.Length);
        foreach (char character in value.ToLowerInvariant())
        {
            sanitized.Append(
                char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-'
                    ? character
                    : '-');
        }
        return sanitized.ToString().Trim('-', '.');
    }

    private static void ValidateCopilotVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version) ||
            !Regex.IsMatch(
                version,
                @"^\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:\.[0-9A-Za-z]+)*)?" +
                @"(?:\+[0-9A-Za-z]+(?:\.[0-9A-Za-z]+)*)?$",
                RegexOptions.CultureInvariant))
        {
            throw new AgentImageException(
                $"Copilot CLI version must be an exact three-part version: {version}");
        }
    }

    private static async Task<string> InspectImageIdAsync(
        string image,
        CancellationToken cancellationToken)
    {
        ProcessResult result = await RunDockerAsync(
            ["image", "inspect", "--format", "{{.Id}}", image],
            cancellationToken);
        string id = result.StandardOutput.Trim();
        if (string.IsNullOrWhiteSpace(id))
            throw new AgentImageException($"Docker image has no identity: {image}");
        return id;
    }

    internal static string BuildIsolatedImageDockerfile(
        string baseImage,
        string trialImage)
    {
        if (string.IsNullOrWhiteSpace(baseImage))
            throw new AgentImageException("base image name is required");
        if (string.IsNullOrWhiteSpace(trialImage))
            throw new AgentImageException("trial image name is required");
        return $"FROM {baseImage}{Environment.NewLine}" +
            $"LABEL com.bartr.wharf.trial-image=\"{trialImage}\"{Environment.NewLine}";
    }

    internal static IReadOnlyList<string> BuildIsolatedImageArguments(
        string dockerfile,
        string trialImage,
        string context) =>
        [
            "image", "build",
            "--pull=false",
            "--file", dockerfile,
            "--tag", trialImage,
            context
        ];

    internal static string ReadBaseImage(string dockerfile)
    {
        foreach (string line in File.ReadLines(dockerfile))
        {
            Match match = Regex.Match(line, @"^\s*FROM\s+([^\s]+)", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value;
        }

        throw new AgentImageException($"Dockerfile has no FROM instruction: {dockerfile}");
    }

    private static async Task<ProcessResult> RunDockerAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
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
            ?? throw new AgentImageException("Docker process could not be started");
        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }
        var result = new ProcessResult(process.ExitCode, await output, await error);
        if (result.ExitCode != 0)
            throw new AgentImageException($"Docker command failed: {SelectError(result)}");
        return result;
    }

    private static string SelectError(ProcessResult result) =>
        string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();

    public async Task RemoveImageAsync(
        string image,
        CancellationToken cancellationToken = default)
    {
        ProcessResult result = await RunDockerAsync(
            ["image", "rm", image],
            cancellationToken);
        DockerRunCleanup.ForgetImage(image);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
    private sealed record CachedImageResult(string Image, bool Reused);
}

public sealed class AgentImageException(string message) : Exception(message);
