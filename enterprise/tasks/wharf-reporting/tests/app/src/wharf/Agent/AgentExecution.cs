using System.Diagnostics;
using System.Text.Json;
using Wharf.State;

namespace Wharf.Agent;

public sealed class AgentExecutionService
{
    private readonly IAgentRunner runner;

    public AgentExecutionService(IAgentRunner runner) =>
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));

    public async Task<AgentEvidenceSummary> ExecuteAsync(
        AgentExecutionRequest request,
        string evidenceDirectory,
        CancellationToken cancellationToken = default)
    {
        request.Validate();
        if (string.IsNullOrWhiteSpace(evidenceDirectory))
        {
            throw new AgentExecutionException("evidence directory must not be empty");
        }

        Directory.CreateDirectory(evidenceDirectory);
        AgentRunResult result = await runner.RunAsync(request, cancellationToken);
        string status = string.IsNullOrWhiteSpace(result.Patch)
            ? "unfinished"
            : result.ExitCode == 0 ? "completed" : "failed";
        AgentEvidenceSummary summary = WriteEvidence(
            evidenceDirectory,
            request.TaskId,
            status,
            result);
        if (status == "unfinished")
        {
            throw new AgentExecutionException(
                $"agent produced no patch for task {request.TaskId}");
        }

        return summary;
    }

    private static AgentEvidenceSummary WriteEvidence(
        string directory,
        string taskId,
        string status,
        AgentRunResult result)
    {
        string stdoutPath = Path.Combine(directory, "stdout.log");
        string stderrPath = Path.Combine(directory, "stderr.log");
        string trajectoryPath = Path.Combine(directory, "trajectory.jsonl");
        string patchPath = Path.Combine(directory, "patch.diff");
        string summaryPath = Path.Combine(directory, "result.json");
        File.WriteAllText(stdoutPath, result.StandardOutput);
        File.WriteAllText(stderrPath, result.StandardError);
        File.WriteAllText(trajectoryPath, result.Trajectory);
        File.WriteAllText(patchPath, result.Patch);
        var summary = new AgentEvidenceSummary(
            AgentEvidenceSchema.Version,
            taskId,
            status,
            result.ExitCode,
            (long)result.Duration.TotalMilliseconds,
            result.Tokens,
            result.Steps,
            result.Requests,
            result.Cost,
            Path.GetFileName(patchPath),
            Path.GetFileName(stdoutPath),
            Path.GetFileName(stderrPath),
            Path.GetFileName(trajectoryPath));
        AtomicFile.Replace(
            summaryPath,
            JsonSerializer.Serialize(summary, StateJson.Options));
        return summary;
    }
}

public sealed class DockerAgentRunner : IAgentRunner
{
    public async Task<AgentRunResult> RunAsync(
        AgentExecutionRequest request,
        CancellationToken cancellationToken)
    {
        request.Validate();
        var arguments = new List<string>
        {
            "run", "--rm", "--user", "1000:1000", "--network", request.Network,
            "--mount", $"type=bind,src={request.SourceDirectory},dst=/task,readonly",
            "--mount", $"type=bind,src={request.WorkspaceDirectory},dst=/workspace",
            request.AgentImage
        };
        arguments.AddRange(request.Invocation.Arguments);
        if (request.Invocation.Environment.TryGetValue(
                "WHARF_COPILOT_CREDENTIAL_VARIABLE",
                out string? credentialVariable) &&
            IsEnvironmentVariable(credentialVariable))
        {
            int insertionPoint = arguments.Count - request.Invocation.Arguments.Count;
            arguments.Insert(insertionPoint, "-e");
            arguments.Insert(insertionPoint + 1, credentialVariable);
        }

        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        using Process process = Process.Start(startInfo)
            ?? throw new AgentExecutionException("Docker agent process could not be started");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        string output = await outputTask;
        string error = await errorTask;
        stopwatch.Stop();
        string patch = await ReadPatchAsync(request.WorkspaceDirectory, cancellationToken);
        return new AgentRunResult(
            process.ExitCode,
            output,
            error,
            output,
            patch,
            stopwatch.Elapsed);
    }

    private static async Task<string> ReadPatchAsync(
        string workspace,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(workspace);
        startInfo.ArgumentList.Add("diff");
        startInfo.ArgumentList.Add("--binary");
        startInfo.ArgumentList.Add("--no-ext-diff");
        using Process process = Process.Start(startInfo)
            ?? throw new AgentExecutionException("git patch capture process could not be started");
        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        string error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            throw new AgentExecutionException($"patch capture failed: {error.Trim()}");
        }

        return output;
    }

    private static bool IsEnvironmentVariable(string value) =>
        value.All(character => char.IsLetterOrDigit(character) || character == '_');
}
