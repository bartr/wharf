using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        if (result.DebugLogDirectory is not null &&
            Directory.Exists(result.DebugLogDirectory))
        {
            CopyDirectory(
                result.DebugLogDirectory,
                Path.Combine(evidenceDirectory, "copilot-logs"));
        }
        if (status == "unfinished")
        {
            throw new AgentExecutionException(
                $"agent produced no patch for task {request.TaskId}");
        }

        return summary;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }

        foreach (string directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(
                directory,
                Path.Combine(destination, Path.GetFileName(directory)));
        }
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
            Path.GetFileName(trajectoryPath),
            result.Usage);
        AtomicFile.Replace(
            summaryPath,
            JsonSerializer.Serialize(summary, StateJson.Options));
        return summary;
    }
}

public sealed partial class DockerAgentRunner : IAgentRunner
{
    public async Task<AgentRunResult> RunAsync(
        AgentExecutionRequest request,
        CancellationToken cancellationToken)
    {
        request.Validate();
        var arguments = new List<string>
        {
            "run", "--rm",
            "--name", request.ContainerName ?? $"agent-{Guid.NewGuid():N}",
            "--user", "1000:1000", "--network", request.Network,
            "--mount", $"type=bind,src={request.SourceDirectory},dst=/task,readonly",
            "--mount", $"type=bind,src={request.WorkspaceDirectory},dst=/workspace",
            "--env", "HOME=/tmp",
            "--env", "XDG_CACHE_HOME=/tmp/.cache"
        };
        if (request.Invocation.Environment.TryGetValue(
                "WHARF_COPILOT_CREDENTIAL_VARIABLE",
                out string? credentialVariable) &&
            IsEnvironmentVariable(credentialVariable))
        {
            arguments.Add("-e");
            arguments.Add(credentialVariable);
        }
        arguments.Add(request.AgentImage);
        arguments.Add("/bin/bash");
        arguments.Add("-lc");
        arguments.Add("cp -a /app/. /workspace/ && cd /workspace/ && exec \"$@\"");
        arguments.Add("--");
        arguments.Add(request.Invocation.Executable);
        arguments.AddRange(request.Invocation.Arguments);

        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (request.Invocation.CredentialValue is not null &&
            request.Invocation.Environment.TryGetValue(
                "WHARF_COPILOT_CREDENTIAL_VARIABLE",
                out string? credentialVariableForProcess) &&
            IsEnvironmentVariable(credentialVariableForProcess))
        {
            startInfo.Environment[credentialVariableForProcess] = request.Invocation.CredentialValue;
        }
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
        string debugLogDirectory = Path.Combine(request.WorkspaceDirectory, ".copilot-logs");
        AgentUsage? usage = CopilotUsageParser.Parse(error, debugLogDirectory);
        string patch = await ReadPatchAsync(request.WorkspaceDirectory, cancellationToken);
        return new AgentRunResult(
            process.ExitCode,
            output,
            error,
            output,
            patch,
            stopwatch.Elapsed,
            Tokens: usage is null ? null : checked((int)usage.TotalTokens),
            Requests: usage?.Requests,
            Cost: usage?.AiCredits,
            DebugLogDirectory: debugLogDirectory,
            Usage: usage);
    }

    internal static partial class CopilotUsageParser
    {
        public static AgentUsage? Parse(string standardError, string logDirectory)
        {
            string logs = standardError;
            if (Directory.Exists(logDirectory))
            {
                foreach (string file in Directory.EnumerateFiles(logDirectory, "*.log", SearchOption.AllDirectories))
                {
                    logs += Environment.NewLine + File.ReadAllText(file);
                }
            }

            var models = new Dictionary<string, AgentModelUsage>(StringComparer.Ordinal);
            foreach (Match match in ModelMetricRegex().Matches(logs))
            {
                string model = match.Groups["model"].Value;
                string metric = match.Groups["metric"].Value;
                long value = long.Parse(match.Groups["value"].Value);
                AgentModelUsage current = models.TryGetValue(model, out AgentModelUsage? existing)
                    ? existing
                    : new AgentModelUsage(0, 0, 0, 0, 0, 0, 0);
                models[model] = metric switch
                {
                    "input_tokens" => current with { InputTokens = value },
                    "output_tokens" => current with { OutputTokens = value },
                    "cache_read_tokens" => current with { CacheReadTokens = value },
                    "cache_write_tokens" => current with { CacheWriteTokens = value },
                    "reasoning_tokens" => current with { ReasoningTokens = value },
                    "request_count" => current with { Requests = checked((int)value) },
                    "request_cost" => current with { RequestCost = decimal.Parse(
                        match.Groups["value"].Value, System.Globalization.CultureInfo.InvariantCulture) },
                    _ => current
                };
            }

            decimal? aiCredits = AiCreditsRegex().Match(standardError) is Match creditMatch &&
                creditMatch.Success
                ? decimal.Parse(creditMatch.Groups["value"].Value, System.Globalization.CultureInfo.InvariantCulture)
                : null;
            if (models.Count == 0 && aiCredits is null)
            {
                return null;
            }

            return new AgentUsage(
                models.Values.Sum(model => model.InputTokens),
                models.Values.Sum(model => model.OutputTokens),
                models.Values.Sum(model => model.CacheReadTokens),
                models.Values.Sum(model => model.CacheWriteTokens),
                models.Values.Sum(model => model.ReasoningTokens),
                models.Values.Sum(model => model.Requests),
                models.Values.Sum(model => model.RequestCost),
                aiCredits,
                models);
        }

        [GeneratedRegex(
            "\"model_(?<model>[^\"]+)_(?<metric>input_tokens|output_tokens|cache_read_tokens|cache_write_tokens|reasoning_tokens|request_count|request_cost)\":\\s+\"?(?<value>[0-9.]+)\"?",
            RegexOptions.CultureInvariant)]
        private static partial Regex ModelMetricRegex();

        [GeneratedRegex(@"AI Credits (?<value>[0-9]+(?:\.[0-9]+)?)", RegexOptions.CultureInvariant)]
        private static partial Regex AiCreditsRegex();
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

        if (!string.IsNullOrWhiteSpace(output))
        {
            return output;
        }

        var committedStartInfo = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        committedStartInfo.ArgumentList.Add("-C");
        committedStartInfo.ArgumentList.Add(workspace);
        committedStartInfo.ArgumentList.Add("diff");
        committedStartInfo.ArgumentList.Add("--binary");
        committedStartInfo.ArgumentList.Add("--no-ext-diff");
        committedStartInfo.ArgumentList.Add("HEAD^");
        using Process committedProcess = Process.Start(committedStartInfo)
            ?? throw new AgentExecutionException("committed patch capture process could not be started");
        string committedOutput = await committedProcess.StandardOutput.ReadToEndAsync(cancellationToken);
        _ = await committedProcess.StandardError.ReadToEndAsync(cancellationToken);
        await committedProcess.WaitForExitAsync(cancellationToken);
        if (committedProcess.ExitCode == 0 && !string.IsNullOrWhiteSpace(committedOutput))
        {
            return committedOutput;
        }

        return output;
    }

    private static bool IsEnvironmentVariable(string value) =>
        value.All(character => char.IsLetterOrDigit(character) || character == '_');

}
