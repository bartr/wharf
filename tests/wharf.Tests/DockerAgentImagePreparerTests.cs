using System.Diagnostics;
using System.Text.Json;
using Wharf.Docker;
using Wharf.Image;
using Wharf.Tasks;
using Wharf.Build;
using Wharf.Configuration;
using Wharf.Logging;
using Wharf.Run;

namespace Wharf.Tests;

public sealed class DockerAgentImagePreparerTests
{
    private const string TaskId = "aiomonitor-task-snapshots-diff";

    [Fact]
    public async Task BuildWarmupIsReusedByAgentAndVerifierPreparationAcrossAllEnterpriseTasks()
    {
        string taskRoot = Path.Combine(FindRepositoryRoot(), "enterprise", "tasks");
        IReadOnlyList<HarborTask> tasks = new HarborTaskLoader().Load(taskRoot);
        Assert.Equal(14, tasks.Count);
        using var logger = WharfLogger.Create(new WharfConfiguration { Quiet = true });
        BuildResult warmed = new BuildService().Execute(taskRoot, logger, 3, "1.0.107");
        Assert.Equal(14, warmed.PreparedCount);
        Assert.Equal(14, warmed.VerifierPreparedCount);

        var preparer = new DockerAgentImagePreparer();
        string evidence = Path.Combine(Path.GetTempPath(), $"wharf-warm-cache-test-{Guid.NewGuid():N}");
        try
        {
            foreach (HarborTask task in tasks)
            {
                string identity = $"cache-verification-{Guid.NewGuid():N}";
                string trial = Path.Combine(evidence, task.TaskId);
                string requested = RunService.BuildAgentImageName($"{task.TaskId}__{identity}");
                string? image = null;
                string? verifier = null;
                var progress = new List<string>();
                try
                {
                    image = await preparer.PrepareAsync(
                        task, Path.Combine(trial, "agent"), requested,
                        progress.Add, copilotVersion: "1.0.107");
                    using JsonDocument cache = JsonDocument.Parse(File.ReadAllText(
                        Path.Combine(trial, "agent", "agent-image-cache.json")));
                    Assert.Equal("cache-hit", cache.RootElement.GetProperty("cliOutcome").GetString());
                    Assert.Equal("cache-hit", cache.RootElement.GetProperty("agentOutcome").GetString());
                    Assert.Contains(progress, message => message.Contains(
                        "task environment cache-hit", StringComparison.Ordinal));
                    Directory.CreateDirectory(Path.Combine(trial, "verifier"));
                    verifier = await RunService.PrepareVerifierImageAsync(
                        task, trial, identity, logger, $"cache-test task={task.TaskId}",
                        1800, CancellationToken.None);
                    Assert.NotNull(verifier);
                    Assert.Contains("CACHED", File.ReadAllText(
                        Path.Combine(trial, "verifier", "image-build.log")), StringComparison.Ordinal);
                }
                finally
                {
                    if (verifier is not null)
                        await preparer.RemoveImageAsync(verifier);
                    if (image is not null)
                        await preparer.RemoveImageAsync(image);
                }
            }
        }
        finally
        {
            if (Directory.Exists(evidence))
                Directory.Delete(evidence, recursive: true);
        }
    }

    [Fact]
    public async Task BuildDoesNotRetagOrDeleteAnExistingEcrParent()
    {
        const string parent = "public.ecr.aws/x8v8d7g8/mars-base:latest";
        Assert.True(await ImageExistsAsync(parent));
        JsonElement original = await InspectImageAsync(parent);
        string evidence = Path.Combine(Path.GetTempPath(), $"wharf-build-ecr-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(evidence);
        string taskDirectory = Path.Combine(evidence, "task");
        Directory.CreateDirectory(taskDirectory);
        string taskId = $"build-ecr-fixture-{Guid.NewGuid():N}";
        string? child = null;
        string? cacheImage = null;
        var preparer = new DockerAgentImagePreparer();
        try
        {
            File.WriteAllText(Path.Combine(taskDirectory, "instruction.md"), "Build-only cache fixture.");
            File.WriteAllText(Path.Combine(taskDirectory, "task.toml"), $$"""
                schema_version = "1.3"
                [task]
                name = "{{taskId}}"
                description = "Build-only cache fixture"
                [metadata]
                task_id = "{{taskId}}"
                display_title = "Build-only fixture"
                category = "enhancement"
                language = "python"
                [environment]
                docker_image = "{{parent}}"
                [agent]
                [verifier]
                [verifier.environment]
                """);
            using var logger = WharfLogger.Create(new WharfConfiguration { Quiet = true });
            BuildResult result = new BuildService().Execute(evidence, logger, 2, "1.0.107");
            Assert.Equal(1, result.PreparedCount);
            Assert.Equal(0, result.VerifierPreparedCount);
            Assert.Equal(original.GetProperty("Id").GetString(),
                (await InspectImageAsync(parent)).GetProperty("Id").GetString());

            HarborTask task = Assert.Single(new HarborTaskLoader().Load(evidence));
            child = RunService.BuildAgentImageName($"{taskId}__verification");
            await preparer.PrepareAsync(task, Path.Combine(evidence, "agent"), child,
                copilotVersion: "1.0.107");
            using JsonDocument cache = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(evidence, "agent", "agent-image-cache.json")));
            cacheImage = cache.RootElement.GetProperty("agentImage").GetString();
            Assert.Equal("cache-hit", cache.RootElement.GetProperty("agentOutcome").GetString());
            Assert.Equal(original.GetProperty("Id").GetString(),
                (await InspectImageAsync(parent)).GetProperty("Id").GetString());
        }
        finally
        {
            if (child is not null)
                await preparer.RemoveImageAsync(child);
            if (cacheImage is not null)
                await preparer.RemoveImageAsync(cacheImage);
            Directory.Delete(evidence, recursive: true);
        }
    }

    [Fact]
    public void BuildsAgentCacheWithLinkedCliPayload()
    {
        string dockerfile = DockerAgentImagePreparer.BuildAgentCacheDockerfile(
            "registry.example.test/task@sha256:abc",
            "wharf-copilot-cli:1.0.95-linux-amd64-v1",
            "1.0.95",
            "cache-key");

        Assert.Contains(
            "COPY --link --from=cli /opt/wharf/copilot /opt/wharf/copilot",
            dockerfile,
            StringComparison.Ordinal);
        Assert.Contains(
            "ENV PATH=\"/opt/wharf/copilot/node_modules/.bin:${PATH}\"",
            dockerfile,
            StringComparison.Ordinal);
        Assert.Contains(
            "LABEL com.bartr.wharf.cache-key=\"cache-key\"",
            dockerfile,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BuildsVersionedCopilotPayloadWithLinkedScratchLayer()
    {
        string dockerfile = DockerAgentImagePreparer.BuildCopilotPayloadDockerfile(
            "registry.example.test/task:1",
            "1.0.95");

        Assert.Contains("FROM scratch", dockerfile, StringComparison.Ordinal);
        Assert.Contains(
            "npm install --prefix /opt/wharf/copilot --no-fund --no-audit " +
            "@github/copilot@1.0.95",
            dockerfile,
            StringComparison.Ordinal);
        Assert.Contains(
            "COPY --link --from=installer /opt/wharf/copilot /opt/wharf/copilot",
            dockerfile,
            StringComparison.Ordinal);
        Assert.Contains(
            ".version !== '1.0.95'",
            dockerfile,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NamesCopilotCacheByExactVersionPlatformAndRecipe()
    {
        string image = DockerAgentImagePreparer.BuildCopilotCacheImageName("1.0.95");

        Assert.StartsWith("wharf-copilot-cli:1.0.95-linux-", image, StringComparison.Ordinal);
        Assert.EndsWith("-v1", image, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadsRelocatedDeepSweTasks()
    {
        IReadOnlyList<HarborTask> tasks = new HarborTaskLoader()
            .Load(Path.Combine(FindRepositoryRoot(), "deep-swe", "tasks"));

        Assert.Equal(113, tasks.Count);
        Assert.Contains(tasks, task => task.TaskId == TaskId);
    }

    [Fact]
    public void ReadsConfiguredEcrBaseWithoutLocalSubstitution()
    {
        string dockerfile = Path.Combine(
            FindRepositoryRoot(),
            "deep-swe",
            "smoke-tasks",
            "smoke-go",
            "environment",
            "Dockerfile");

        string image = DockerAgentImagePreparer.ReadBaseImage(dockerfile);

        Assert.Equal("public.ecr.aws/x8v8d7g8/mars-base:latest", image);
    }

    [Fact]
    public void SanitizesSupportedVersionMetadataForDockerTag()
    {
        string image = DockerAgentImagePreparer.BuildCopilotCacheImageName("1.2.3-rc.1+build.4");

        Assert.StartsWith(
            "wharf-copilot-cli:1.2.3-rc.1-build.4-linux-",
            image,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnvironmentImageTaskBuildsAndRemovesUniqueTrialImage()
    {
        string repositoryRoot = FindRepositoryRoot();
        HarborTask task = new HarborTaskLoader()
            .Load(Path.Combine(repositoryRoot, "deep-swe", "tasks"))
            .Single(task => task.TaskId == TaskId);
        string image = $"agent-{TaskId}__image-smoke-{Guid.NewGuid():N}:latest";
        string evidenceDirectory = Path.Combine(
            Path.GetTempPath(),
            $"wharf-{TaskId}-{Guid.NewGuid():N}");
        var preparer = new DockerAgentImagePreparer();

        string environmentImage = Assert.IsType<string>(task.EnvironmentImage);
        Assert.Equal(
            "public.ecr.aws/d3j8x8q7/swe-bench-202605:" +
            "kh75rc2q0zhmsqwk7wewfwwtrx830v2n-v1.1",
            environmentImage);

        try
        {
            string prepared = await preparer.PrepareAsync(
                task,
                evidenceDirectory,
                image);

            Assert.Equal(image, prepared);
            Assert.Contains(
                $"FROM {environmentImage}",
                File.ReadAllText(Path.Combine(
                    evidenceDirectory,
                    "Dockerfile.wharf")),
                StringComparison.Ordinal);

            JsonElement parent = await InspectImageAsync(environmentImage);
            JsonElement child = await InspectImageAsync(image);
            Assert.NotEqual(
                parent.GetProperty("Id").GetString(),
                child.GetProperty("Id").GetString());
            Assert.Equal(
                image,
                child.GetProperty("Config")
                    .GetProperty("Labels")
                    .GetProperty("com.bartr.wharf.trial-image")
                    .GetString());

            await preparer.RemoveImageAsync(image);

            Assert.False(await ImageExistsAsync(image));
            Assert.True(await ImageExistsAsync(environmentImage));
        }
        finally
        {
            await RunDockerAsync(["image", "rm", "--force", image], allowFailure: true);
            DockerRunCleanup.ForgetImage(image);
            if (Directory.Exists(evidenceDirectory))
                Directory.Delete(evidenceDirectory, recursive: true);
        }
    }

    private static async Task<JsonElement> InspectImageAsync(string image)
    {
        ProcessResult result = await RunDockerAsync(["image", "inspect", image]);
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        return document.RootElement[0].Clone();
    }

    private static async Task<bool> ImageExistsAsync(string image) =>
        (await RunDockerAsync(
            ["image", "inspect", image],
            allowFailure: true)).ExitCode == 0;

    private static async Task<ProcessResult> RunDockerAsync(
        IReadOnlyList<string> arguments,
        bool allowFailure = false)
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
            ?? throw new InvalidOperationException("Docker process could not be started");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var result = new ProcessResult(process.ExitCode, await output, await error);
        if (!allowFailure && result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(result.StandardError)
                    ? result.StandardOutput.Trim()
                    : result.StandardError.Trim());
        }

        return result;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !Directory.Exists(Path.Combine(
                   directory.FullName,
                   "deep-swe",
                   "tasks",
                   TaskId)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("repository root not found");
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
