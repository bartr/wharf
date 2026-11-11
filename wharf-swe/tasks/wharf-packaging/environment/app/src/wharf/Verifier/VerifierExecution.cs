using System.Diagnostics;
using System.Text.Json;
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

        string status = results.Any(result => result.TimedOut || result.ExitCode != 0)
            ? "failed"
            : "passed";
        var trial = new TrialResult(
            TrialResultSchema.Version,
            request.TaskId,
            status,
            workspace,
            results,
            Path.Combine(evidenceDirectory, "patch.diff"));
        File.WriteAllText(Path.Combine(evidenceDirectory, "patch.diff"), patch);
        AtomicFile.Replace(
            Path.Combine(evidenceDirectory, "trial-result.json"),
            JsonSerializer.Serialize(trial, StateJson.Options));
        return trial;
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
