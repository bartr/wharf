using System.Diagnostics;
using System.Text.Json;
using Wharf.Agent;
using Wharf.State;

namespace Wharf.Verifier;

public sealed class PristineVerifierService
{
    private readonly IPristineWorkspaceFactory workspaceFactory;
    private readonly IPatchApplier patchApplier;
    private readonly IVerifierRunner runner;

    public PristineVerifierService(
        IPristineWorkspaceFactory workspaceFactory,
        IPatchApplier patchApplier,
        IVerifierRunner runner)
    {
        this.workspaceFactory = workspaceFactory;
        this.patchApplier = patchApplier;
        this.runner = runner;
    }

    public async Task<TrialResult> VerifyAsync(
        VerifierRequest request,
        string patch,
        string evidenceDirectory,
        CancellationToken cancellationToken = default)
    {
        PreparedVerifierRun prepared = await PrepareAsync(
            request,
            patch,
            evidenceDirectory,
            cancellationToken);
        return await VerifyPreparedAsync(prepared, cancellationToken);
    }

    public async Task<PreparedVerifierRun> PrepareAsync(
        VerifierRequest request,
        string patch,
        string evidenceDirectory,
        CancellationToken cancellationToken = default)
    {
        request.Validate();
        if (string.IsNullOrWhiteSpace(patch))
        {
            throw new VerifierExecutionException("retained patch must not be empty");
        }

        Directory.CreateDirectory(evidenceDirectory);
        string workspace = await workspaceFactory.CreateAsync(
            request.SourceDirectory,
            cancellationToken);
        if (Path.GetFullPath(workspace).Equals(
                Path.GetFullPath(request.AgentWorkspaceDirectory),
                StringComparison.Ordinal))
        {
            throw new VerifierExecutionException("pristine workspace reused the agent workspace");
        }

        await patchApplier.ApplyAsync(workspace, patch, cancellationToken);
        return new PreparedVerifierRun(
            request,
            patch,
            evidenceDirectory,
            workspace);
    }

    public async Task<TrialResult> VerifyPreparedAsync(
        PreparedVerifierRun prepared,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        VerifierRequest request = prepared.Request;
        string evidenceDirectory = prepared.EvidenceDirectory;
        string workspace = prepared.WorkspaceDirectory;
        var results = new List<VerifierCommandResult>();
        foreach (VerifierCommand command in request.Commands)
        {
            VerifierCommandResult result = await runner.RunAsync(
                request,
                workspace,
                command,
                cancellationToken);
            results.Add(result);
            WriteCommandEvidence(evidenceDirectory, result);
            if (result.TimedOut || result.ExitCode != 0)
            {
                break;
            }
        }

        VerifierMetrics? metrics = ReadMetrics(evidenceDirectory);
        string status = results.Any(result => result.TimedOut || result.ExitCode != 0) ||
            metrics is { PassAt: 0 }
                ? "failed"
                : "passed";
        var trial = new TrialResult(
            TrialResultSchema.Version,
            request.TaskId,
            status,
            workspace,
            results,
            Path.Combine(evidenceDirectory, "patch.diff"),
            metrics);
        File.WriteAllText(Path.Combine(evidenceDirectory, "patch.diff"), prepared.Patch);
        AtomicFile.Replace(
            Path.Combine(evidenceDirectory, "trial-result.json"),
            JsonSerializer.Serialize(trial, StateJson.Options));
        VerifierCommandResult? timeout = results.FirstOrDefault(result => result.TimedOut);
        if (timeout is not null)
        {
            throw new VerifierExecutionException(
                $"verifier command '{timeout.Name}' exceeded its timeout",
                "VerifierTimeout");
        }

        VerifierCommandResult? terminated = results.FirstOrDefault(result =>
            result.ExitCode is 137 or 143);
        if (terminated is not null)
        {
            throw new VerifierExecutionException(
                $"verifier command '{terminated.Name}' was externally terminated " +
                $"with exit code {terminated.ExitCode}",
                "VerifierInfrastructure");
        }

        return trial;
    }

    private static VerifierMetrics? ReadMetrics(string evidenceDirectory)
    {
        string path = Path.Combine(evidenceDirectory, "reward.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = document.RootElement;
            int passAt = root.TryGetProperty("reward", out JsonElement reward)
                ? reward.GetInt32()
                : 0;
            double f2p = root.TryGetProperty("f2p", out JsonElement f2pValue)
                ? f2pValue.GetDouble() * 100
                : 0;
            double p2p = root.TryGetProperty("p2p", out JsonElement p2pValue)
                ? p2pValue.GetDouble() * 100
                : 0;
            return new VerifierMetrics(passAt, f2p, p2p);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void WriteCommandEvidence(
        string directory,
        VerifierCommandResult result)
    {
        string prefix = Path.Combine(directory, result.Name);
        File.WriteAllText($"{prefix}.stdout.log", result.StandardOutput);
        File.WriteAllText($"{prefix}.stderr.log", result.StandardError);
    }
}

public sealed class FilePristineWorkspaceFactory : IPristineWorkspaceFactory
{
    public Task<string> CreateAsync(
        string sourceDirectory,
        CancellationToken cancellationToken)
    {
        string workspace = Path.Combine(
            Path.GetTempPath(),
            $"wharf-verifier-{Guid.NewGuid():N}");
        CopyDirectory(sourceDirectory, workspace);
        return Task.FromResult(workspace);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            string target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}

public sealed class GitPatchApplier : IPatchApplier
{
    public async Task ApplyAsync(
        string workspaceDirectory,
        string patch,
        CancellationToken cancellationToken)
    {
        string patchPath = Path.Combine(workspaceDirectory, ".wharf.patch");
        await File.WriteAllTextAsync(patchPath, patch, cancellationToken);
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workspaceDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("apply");
        startInfo.ArgumentList.Add("--no-index");
        startInfo.ArgumentList.Add("--unsafe-paths");
        startInfo.ArgumentList.Add(patchPath);
        using Process process = Process.Start(startInfo)
            ?? throw new VerifierExecutionException("git patch process could not be started");
        string error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        File.Delete(patchPath);
        if (process.ExitCode != 0)
        {
            throw new VerifierExecutionException($"patch application failed: {error.Trim()}");
        }
    }
}

public sealed class RetainingPatchApplier : IPatchApplier
{
    public Task ApplyAsync(
        string workspaceDirectory,
        string patch,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

public sealed class ProcessVerifierRunner : IVerifierRunner
{
    public async Task<VerifierCommandResult> RunAsync(
        VerifierRequest request,
        string workspaceDirectory,
        VerifierCommand command,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("/bin/sh")
        {
            WorkingDirectory = workspaceDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(command.Command);
        foreach (string variable in request.CredentialEnvironmentVariables)
        {
            startInfo.Environment[variable] = Environment.GetEnvironmentVariable(variable) ?? string.Empty;
        }

        using Process process = Process.Start(startInfo)
            ?? throw new VerifierExecutionException("verifier process could not be started");
        var stopwatch = Stopwatch.StartNew();
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(command.Timeout);
        bool timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
        }

        stopwatch.Stop();
        return new VerifierCommandResult(
            command.Name,
            process.ExitCode,
            await outputTask,
            await errorTask,
            (long)stopwatch.Elapsed.TotalMilliseconds,
            timedOut);
    }
}

public sealed class DockerVerifierRunner : IVerifierRunner
{
    public async Task<VerifierCommandResult> RunAsync(
        VerifierRequest request,
        string workspaceDirectory,
        VerifierCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Image))
        {
            throw new VerifierExecutionException("Docker verifier image is required");
        }

        string containerName =
            $"verifier-{SanitizeContainerName(request.TaskId)}-{Guid.NewGuid():N}";
        bool created = false;
        string evidenceDirectory = Path.GetFullPath(
            request.EvidenceDirectory
            ?? throw new VerifierExecutionException(
                "verifier evidence directory is required for Docker verification"));
        string artifactsDirectory = Path.GetFullPath(
            request.ArtifactsDirectory
            ?? throw new VerifierExecutionException(
                "verifier artifacts directory is required for Docker verification"));
        bool evidenceCopied = false;
        try
        {
            DockerProcessResult creation = await DockerAgentProcess.RunAsync(
                BuildArguments(request, command, containerName),
                credentialVariable: null,
                credentialValue: null,
                cancellationToken);
            EnsureSuccessful(creation, "verifier container creation");
            created = true;

            DockerProcessResult start = await DockerAgentProcess.RunAsync(
                ["start", containerName],
                credentialVariable: null,
                credentialValue: null,
                cancellationToken);
            EnsureSuccessful(start, "verifier container start");
            DockerProcessResult initialization = await DockerAgentProcess.RunAsync(
                [
                    "exec", "--user", "0:0", containerName,
                    "/bin/bash", "-lc",
                    "rm -rf /logs/verifier /logs/artifacts /logs/wharf-command && " +
                    "mkdir -p /logs/verifier /logs/artifacts /logs/wharf-command"
                ],
                credentialVariable: null,
                credentialValue: null,
                cancellationToken);
            EnsureSuccessful(initialization, "verifier container initialization");
            DockerProcessResult artifactCopy = await DockerAgentProcess.RunAsync(
                ["cp", $"{artifactsDirectory}/.", $"{containerName}:/logs/artifacts/"],
                credentialVariable: null,
                credentialValue: null,
                cancellationToken);
            EnsureSuccessful(artifactCopy, "verifier artifact copy");

            IReadOnlyList<string> executionArguments =
                BuildExecutionArguments(command, containerName);
            var startInfo = new ProcessStartInfo("docker")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (string argument in executionArguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(startInfo)
                ?? throw new VerifierExecutionException(
                    "Docker verifier process could not be started");
            var stopwatch = Stopwatch.StartNew();
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync(
                cancellationToken);
            Task<string> errorTask = process.StandardError.ReadToEndAsync(
                cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeout.CancelAfter(command.Timeout);
            bool timedOut = false;
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                timedOut = true;
                await DockerContainerControl.StopAsync(
                    containerName,
                    CancellationToken.None);
                await process.WaitForExitAsync(CancellationToken.None);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await DockerContainerControl.StopAsync(
                    containerName,
                    CancellationToken.None);
                await process.WaitForExitAsync(CancellationToken.None);
                throw;
            }

            stopwatch.Stop();
            _ = await outputTask;
            _ = await errorTask;
            await CopyVerifierEvidenceAsync(
                containerName,
                evidenceDirectory,
                CancellationToken.None);
            await CopyCommandEvidenceAsync(
                containerName,
                evidenceDirectory,
                CancellationToken.None);
            evidenceCopied = true;
            string prefix = Path.Combine(evidenceDirectory, command.Name);
            return new VerifierCommandResult(
                command.Name,
                process.ExitCode,
                ReadRequiredEvidence($"{prefix}.stdout.log"),
                ReadRequiredEvidence($"{prefix}.stderr.log"),
                (long)stopwatch.Elapsed.TotalMilliseconds,
                timedOut);
        }
        finally
        {
            if (created)
            {
                if (!evidenceCopied)
                {
                    await CopyVerifierEvidenceIfPresentAsync(
                        containerName,
                        evidenceDirectory);
                    await CopyCommandEvidenceIfPresentAsync(
                        containerName,
                        evidenceDirectory);
                }
                DockerProcessResult removal = await DockerAgentProcess.RunAsync(
                    ["rm", "--force", containerName],
                    credentialVariable: null,
                    credentialValue: null,
                    CancellationToken.None);
                if (removal.ExitCode != 0 &&
                    !removal.StandardError.Contains(
                        "No such container",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new VerifierExecutionException(
                        $"verifier container cleanup failed: " +
                        removal.StandardError.Trim());
                }
            }
        }
    }

    internal static IReadOnlyList<string> BuildArguments(
        VerifierRequest request,
        VerifierCommand command,
        string? containerName = null)
    {
        if (string.IsNullOrWhiteSpace(request.Image))
        {
            throw new VerifierExecutionException("Docker verifier image is required");
        }

        var arguments = new List<string>
        {
            "create",
            "--label", "com.bartr.wharf.managed=true",
        };
        if (!string.IsNullOrWhiteSpace(containerName))
        {
            arguments.Add("--name");
            arguments.Add(containerName);
        }

        arguments.AddRange(
        [
            "--network", request.Network,
            "--memory", "8g",
            "--entrypoint", "/bin/bash",
            request.Image,
            "-lc",
            "exec sleep infinity"
        ]);
        return arguments;
    }

    internal static IReadOnlyList<string> BuildExecutionArguments(
        VerifierCommand command,
        string containerName)
    {
        string logName = SanitizeContainerName(command.Name);
        return
        [
            "exec",
            containerName,
            "/bin/bash",
            "-lc",
            $"eval \"$1\" >'/logs/wharf-command/{logName}.stdout.log' " +
            $"2>'/logs/wharf-command/{logName}.stderr.log'",
            "--",
            command.Command
        ];
    }

    private static async Task CopyVerifierEvidenceAsync(
        string containerName,
        string evidenceDirectory,
        CancellationToken cancellationToken)
    {
        DockerProcessResult copy = await DockerAgentProcess.RunAsync(
            ["cp", $"{containerName}:/logs/verifier/.", evidenceDirectory],
            credentialVariable: null,
            credentialValue: null,
            cancellationToken);
        EnsureSuccessful(copy, "verifier evidence copy");
    }

    private static async Task CopyCommandEvidenceAsync(
        string containerName,
        string evidenceDirectory,
        CancellationToken cancellationToken)
    {
        DockerProcessResult copy = await DockerAgentProcess.RunAsync(
            ["cp", $"{containerName}:/logs/wharf-command/.", evidenceDirectory],
            credentialVariable: null,
            credentialValue: null,
            cancellationToken);
        EnsureSuccessful(copy, "verifier command evidence copy");
    }

    private static async Task CopyVerifierEvidenceIfPresentAsync(
        string containerName,
        string evidenceDirectory)
    {
        DockerProcessResult copy = await DockerAgentProcess.RunAsync(
            ["cp", $"{containerName}:/logs/verifier/.", evidenceDirectory],
            credentialVariable: null,
            credentialValue: null,
            CancellationToken.None);
        if (copy.ExitCode != 0 &&
            !copy.StandardError.Contains(
                "Could not find",
                StringComparison.OrdinalIgnoreCase) &&
            !copy.StandardError.Contains(
                "No such container",
                StringComparison.OrdinalIgnoreCase))
        {
            EnsureSuccessful(copy, "verifier evidence copy");
        }
    }

    private static async Task CopyCommandEvidenceIfPresentAsync(
        string containerName,
        string evidenceDirectory)
    {
        DockerProcessResult copy = await DockerAgentProcess.RunAsync(
            ["cp", $"{containerName}:/logs/wharf-command/.", evidenceDirectory],
            credentialVariable: null,
            credentialValue: null,
            CancellationToken.None);
        if (copy.ExitCode != 0 &&
            !copy.StandardError.Contains(
                "Could not find",
                StringComparison.OrdinalIgnoreCase) &&
            !copy.StandardError.Contains(
                "No such container",
                StringComparison.OrdinalIgnoreCase))
        {
            EnsureSuccessful(copy, "verifier command evidence copy");
        }
    }

    private static void EnsureSuccessful(DockerProcessResult result, string stage)
    {
        if (result.ExitCode == 0)
        {
            return;
        }

        string diagnostic = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();
        throw new VerifierExecutionException(
            string.IsNullOrWhiteSpace(diagnostic)
                ? $"{stage} failed with exit code {result.ExitCode}"
                : $"{stage} failed: {diagnostic}");
    }

    private static string ReadRequiredEvidence(string path)
    {
        if (!File.Exists(path))
        {
            throw new VerifierExecutionException(
                $"verifier container did not produce required evidence: " +
                Path.GetFileName(path));
        }

        return File.ReadAllText(path);
    }

    private static string SanitizeContainerName(string value) =>
        string.Concat(value.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_'
                ? character
                : '-'));
}
