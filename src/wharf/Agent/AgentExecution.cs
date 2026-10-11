using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Wharf.Execution;
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
        AgentRunResult result = await runner.RunAsync(
            request with { EvidenceDirectory = evidenceDirectory },
            cancellationToken);
        string status = result.OutOfMemory || result.TimedOut
            ? "failed"
            : result.ExitCode != 0
            ? "failed"
            : string.IsNullOrWhiteSpace(result.Patch) ? "unfinished" : "completed";
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
                $"agent produced no patch for task {request.TaskId}",
                "AgentUnfinished");
        }

        return summary;
    }

    public async Task<AgentExecutionOutcome> ExecuteWithInfrastructureRetryAsync(
        AgentExecutionRequest request,
        string evidenceDirectory,
        string logicalAttemptId,
        int maximumRetries,
        Func<AgentExecutionRequest, int, CancellationToken, Task<AgentExecutionRequest>> prepareRetry,
        IInfrastructureRetrySink retrySink,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumRetries);
        decimal? totalCost = null;
        int retryNumber = 0;
        while (true)
        {
            AgentExecutionException? executionException = null;
            AgentEvidenceSummary summary;
            try
            {
                summary = await ExecuteAsync(
                    request,
                    evidenceDirectory,
                    cancellationToken);
            }
            catch (AgentExecutionException exception)
            {
                executionException = exception;
                summary = JsonSerializer.Deserialize<AgentEvidenceSummary>(
                    File.ReadAllText(Path.Combine(evidenceDirectory, "result.json")),
                    StateJson.Options)
                    ?? throw new AgentExecutionException(
                        "retained agent result could not be read for infrastructure retry");
            }
            if (summary.Cost is not null)
            {
                totalCost = (totalCost ?? 0) + summary.Cost;
            }

            string standardError = File.ReadAllText(
                Path.Combine(evidenceDirectory, summary.StandardErrorPath));
            if (summary.Status == "completed" ||
                !IsRetryableCopilotServiceFailure(standardError) ||
                retryNumber >= maximumRetries)
            {
                if (executionException is not null)
                {
                    throw executionException;
                }

                return new AgentExecutionOutcome(
                    summary,
                    request,
                    totalCost,
                    retryNumber);
            }

            retryNumber++;
            string retryId = Guid.NewGuid().ToString("N");
            ArchiveEvidence(evidenceDirectory, retryId);
            await retrySink.RecordAsync(
                new InfrastructureRetryEvidence(
                    "1.0",
                    retryId,
                    logicalAttemptId,
                    ExecutionPhase.Agent,
                    retryNumber,
                    "copilot-service",
                    "retrying",
                    standardError.Trim()),
                cancellationToken);
            request = await prepareRetry(
                request,
                retryNumber,
                cancellationToken);
        }
    }

    internal static bool IsRetryableCopilotServiceFailure(string standardError) =>
        standardError.Contains(
            "Execution failed: 400 internal server error",
            StringComparison.OrdinalIgnoreCase);

    private static void ArchiveEvidence(string evidenceDirectory, string retryId)
    {
        string retryDirectory = Path.Combine(
            evidenceDirectory,
            "infrastructure-retries",
            retryId);
        Directory.CreateDirectory(retryDirectory);
        foreach (string name in
            new[] { "result.json", "stdout.log", "stderr.log", "trajectory.jsonl", "patch.diff" })
        {
            string source = Path.Combine(evidenceDirectory, name);
            if (File.Exists(source))
            {
                File.Copy(source, Path.Combine(retryDirectory, name));
            }
        }

        string logs = Path.Combine(evidenceDirectory, "copilot-logs");
        if (Directory.Exists(logs))
        {
            CopyDirectory(logs, Path.Combine(retryDirectory, "copilot-logs"));
            Directory.Delete(logs, recursive: true);
        }
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
            result.Usage,
            result.OutOfMemory
                ? "OutOfMemory"
                : result.TimedOut ? "AgentTimeout" : null,
            result.OutOfMemory
                ? "agent container reported a cgroup OOM kill"
                : result.TimedOut ? "agent execution exceeded its timeout" : null);
        AtomicFile.Replace(
            summaryPath,
            JsonSerializer.Serialize(summary, StateJson.Options));
        return summary;
    }
}

public sealed partial class DockerAgentRunner : IAgentRunner
{
    private readonly IDockerOomMonitor oomMonitor;

    public DockerAgentRunner()
        : this(new DockerEventOomMonitor())
    {
    }

    internal DockerAgentRunner(IDockerOomMonitor oomMonitor) =>
        this.oomMonitor = oomMonitor;

    public async Task<AgentRunResult> RunAsync(
        AgentExecutionRequest request,
        CancellationToken cancellationToken)
    {
        request.Validate();
        string containerName = request.ContainerName ?? $"agent-{Guid.NewGuid():N}";
        Stopwatch stopwatch = Stopwatch.StartNew();
        IReadOnlyList<string> arguments = BuildAgentArguments(request, containerName);
        string? credentialVariable = request.Invocation.Environment.TryGetValue(
                "WHARF_COPILOT_CREDENTIAL_VARIABLE",
                out string? configuredCredentialVariable) &&
            IsEnvironmentVariable(configuredCredentialVariable)
                ? configuredCredentialVariable
                : null;
        string? baseCommit = null;
        try
        {
            baseCommit = await ReadContainerHeadAsync(
                containerName,
                cancellationToken);
            await using IDockerOomWatch oomWatch = oomMonitor.Start(containerName);
            Task<DockerProcessResult> agentTask = DockerAgentProcess.RunAsync(
                arguments,
                credentialVariable,
                request.Invocation.CredentialValue,
                CancellationToken.None);
            TimeSpan agentTimeout = request.Timeout ?? TimeSpan.FromSeconds(5400);
            Task timeoutTask = Task.Delay(agentTimeout, cancellationToken);
            Task<bool> oomTask = oomWatch.WaitAsync(agentTask, CancellationToken.None);
            bool timedOut = false;
            try
            {
                Task completed = await Task.WhenAny(agentTask, oomTask, timeoutTask);
                if (completed == timeoutTask)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    timedOut = true;
                    await DockerContainerControl.StopAsync(containerName, CancellationToken.None);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await DockerContainerControl.StopAsync(containerName, CancellationToken.None);
                try
                {
                    await agentTask;
                }
                catch
                {
                }
                await CaptureContainerEvidenceAsync(
                    request,
                    containerName,
                    baseCommit,
                    CancellationToken.None);
                throw;
            }

            DockerProcessResult agent = await agentTask;
            bool outOfMemory = await oomTask;
            await CaptureContainerEvidenceAsync(
                request,
                containerName,
                baseCommit,
                cancellationToken);
            string evidenceDirectory = request.EvidenceDirectory
                ?? throw new AgentExecutionException(
                    "agent evidence directory is required for Docker execution");

            stopwatch.Stop();
            string standardOutput = ReadRequiredEvidence(
                evidenceDirectory,
                "stdout.log");
            string standardError = ReadRequiredEvidence(
                evidenceDirectory,
                "stderr.log");
            string trajectory = ReadRequiredEvidence(
                evidenceDirectory,
                "trajectory.jsonl");
            string patch = ReadRequiredEvidence(
                evidenceDirectory,
                "patch.diff");
            string debugLogDirectory = Path.Combine(
                evidenceDirectory,
                "copilot-logs");
            AgentUsage? usage = CopilotUsageParser.Parse(
                standardError,
                debugLogDirectory);
            return new AgentRunResult(
                agent.ExitCode,
                standardOutput,
                standardError,
                trajectory,
                patch,
                stopwatch.Elapsed,
                Tokens: usage is null ? null : checked((int)usage.TotalTokens),
                Requests: usage?.Requests,
                Cost: usage?.AiCredits,
                Usage: usage,
                OutOfMemory: outOfMemory,
                TimedOut: timedOut);
        }
        finally
        {
            await RemoveContainerAsync(containerName);
        }
    }

    private static async Task CaptureContainerEvidenceAsync(
        AgentExecutionRequest request,
        string containerName,
        string baseCommit,
        CancellationToken cancellationToken)
    {
        await EnsureContainerRunningAsync(containerName, cancellationToken);
        DockerProcessResult capture = await DockerAgentProcess.RunAsync(
            BuildPatchCaptureArguments(containerName, baseCommit),
            credentialVariable: null,
            credentialValue: null,
            cancellationToken);
        DockerAgentWorkspacePreparer.EnsureSuccessful(
            capture,
            "agent patch capture");
        string evidenceDirectory = request.EvidenceDirectory
            ?? throw new AgentExecutionException(
                "agent evidence directory is required for Docker execution");
        DockerProcessResult evidenceCopy = await DockerAgentProcess.RunAsync(
            ["cp", $"{containerName}:/logs/agent/.", evidenceDirectory],
            credentialVariable: null,
            credentialValue: null,
            cancellationToken);
        DockerAgentWorkspacePreparer.EnsureSuccessful(
            evidenceCopy,
            "agent evidence copy");
        if (request.RetainWorkspace)
        {
            DockerProcessResult workspaceCopy = await DockerAgentProcess.RunAsync(
                ["cp", $"{containerName}:/workspace/.", request.WorkspaceDirectory],
                credentialVariable: null,
                credentialValue: null,
                cancellationToken);
            DockerAgentWorkspacePreparer.EnsureSuccessful(
                workspaceCopy,
                "agent workspace copy");
        }
    }

    internal static IReadOnlyList<string> BuildAgentArguments(
        AgentExecutionRequest request,
        string containerName)
    {
        var arguments = new List<string>
        {
            "exec",
            "--user", "1000:1000",
            "--env", "HOME=/home/wharf",
            "--env", "XDG_CACHE_HOME=/home/wharf/.cache",
            "--env", "NPM_CONFIG_MANAGE_PACKAGE_MANAGER_VERSIONS=false"
        };
        if (request.Invocation.Environment.TryGetValue(
                "WHARF_COPILOT_CREDENTIAL_VARIABLE",
                out string? credentialVariable) &&
            IsEnvironmentVariable(credentialVariable))
        {
            arguments.Add("-e");
            arguments.Add(credentialVariable);
        }
        arguments.Add(containerName);
        arguments.Add("/bin/bash");
        arguments.Add("-lc");
        arguments.Add(
            "export PATH=/opt/wharf/copilot/node_modules/.bin:$PATH; " +
            "cd /workspace/; " +
            "\"$@\" >/logs/agent/stdout.log 2>/logs/agent/stderr.log; " +
            "status=$?; cp /logs/agent/stdout.log /logs/agent/trajectory.jsonl; " +
            "exit $status");
        arguments.Add("--");
        arguments.Add(request.Invocation.Executable);
        arguments.AddRange(request.Invocation.Arguments);
        return arguments;
    }

    internal static IReadOnlyList<string> BuildPatchCaptureArguments(
        string containerName,
        string baseCommit) =>
        [
            "exec",
            "--user", "1000:1000",
            containerName,
            "/bin/bash",
            "-lc",
            """
            set -euo pipefail
            cd /workspace
            touch /logs/agent/stdout.log /logs/agent/stderr.log
            cp /logs/agent/stdout.log /logs/agent/trajectory.jsonl
            patch=/logs/agent/patch.diff
            git diff --binary --no-ext-diff "$1" >"$patch"
            while IFS= read -r -d '' path; do
                case "$path" in
                    .cache|.cache/*|.home|.home/*|.wharf-copilot|.wharf-copilot/*|.copilot-logs|.copilot-logs/*|.wharf-copilot-install.log)
                        continue
                        ;;
                esac
                status=0
                git diff --binary --no-index -- /dev/null "$path" >>"$patch" || status=$?
                if [ "$status" -ne 0 ] && [ "$status" -ne 1 ]; then
                    exit "$status"
                fi
            done < <(git ls-files --others --exclude-standard -z)
            if [ -d /workspace/.copilot-logs ]; then
                cp -a /workspace/.copilot-logs /logs/agent/copilot-logs
            fi
            """,
            "--",
            baseCommit
        ];

    private static async Task<string> ReadContainerHeadAsync(
        string containerName,
        CancellationToken cancellationToken)
    {
        DockerProcessResult result = await DockerAgentProcess.RunAsync(
            [
                "exec",
                "--user", "1000:1000",
                containerName,
                "git", "-C", "/workspace", "rev-parse", "HEAD"
            ],
            credentialVariable: null,
            credentialValue: null,
            cancellationToken);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            throw new AgentExecutionException(
                $"agent workspace HEAD could not be read: {result.StandardError.Trim()}");
        }

        return result.StandardOutput.Trim();
    }

    private static async Task EnsureContainerRunningAsync(
        string containerName,
        CancellationToken cancellationToken)
    {
        DockerProcessResult inspection = await DockerAgentProcess.RunAsync(
            ["inspect", "--format", "{{.State.Running}}", containerName],
            credentialVariable: null,
            credentialValue: null,
            cancellationToken);
        DockerAgentWorkspacePreparer.EnsureSuccessful(
            inspection,
            "agent container inspection");
        if (string.Equals(
                inspection.StandardOutput.Trim(),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        DockerProcessResult start = await DockerAgentProcess.RunAsync(
            ["start", containerName],
            credentialVariable: null,
            credentialValue: null,
            cancellationToken);
        DockerAgentWorkspacePreparer.EnsureSuccessful(
            start,
            "agent evidence restart");
    }

    private static async Task RemoveContainerAsync(string containerName)
    {
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
            throw new AgentExecutionException(
                $"agent container cleanup failed: {removal.StandardError.Trim()}");
        }
    }

    private static string ReadRequiredEvidence(string directory, string name)
    {
        string path = Path.Combine(directory, name);
        if (!File.Exists(path))
        {
            throw new AgentExecutionException(
                $"agent container did not produce required evidence: {name}");
        }

        return File.ReadAllText(path);
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
                    "request_cost" => current with
                    {
                        RequestCost = decimal.Parse(
                        match.Groups["value"].Value, System.Globalization.CultureInfo.InvariantCulture)
                    },
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

    private static async Task<string> ReadHeadAsync(
        string workspace,
        CancellationToken cancellationToken)
    {
        GitProcessResult result = await RunGitAsync(
            workspace,
            ["rev-parse", "HEAD"],
            cancellationToken);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            throw new AgentExecutionException(
                $"agent workspace HEAD could not be read: {result.StandardError.Trim()}");
        }

        return result.StandardOutput.Trim();
    }

    internal static async Task<string> ReadPatchAsync(
        string workspace,
        string baseCommit,
        CancellationToken cancellationToken)
    {
        GitProcessResult tracked = await RunGitAsync(
            workspace,
            ["diff", "--binary", "--no-ext-diff", baseCommit],
            cancellationToken);
        if (tracked.ExitCode != 0)
        {
            throw new AgentExecutionException(
                $"patch capture failed: {tracked.StandardError.Trim()}");
        }

        GitProcessResult untracked = await RunGitAsync(
            workspace,
            ["ls-files", "--others", "--exclude-standard", "-z"],
            cancellationToken);
        if (untracked.ExitCode != 0)
        {
            throw new AgentExecutionException(
                $"untracked file discovery failed: {untracked.StandardError.Trim()}");
        }

        var patch = new System.Text.StringBuilder(tracked.StandardOutput);
        foreach (string path in untracked.StandardOutput.Split(
                     '\0',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (IsWharfRuntimePath(path))
            {
                continue;
            }

            GitProcessResult added = await RunGitAsync(
                workspace,
                ["diff", "--binary", "--no-index", "--", "/dev/null", path],
                cancellationToken);
            if (added.ExitCode is not 0 and not 1)
            {
                throw new AgentExecutionException(
                    $"untracked patch capture failed for {path}: {added.StandardError.Trim()}");
            }

            patch.Append(added.StandardOutput);
        }

        return patch.ToString();
    }

    private static bool IsWharfRuntimePath(string path)
    {
        string first = path.Split('/', 2)[0];
        return first is ".cache" or ".home" or ".wharf-copilot" or ".copilot-logs" ||
            path == ".wharf-copilot-install.log";
    }

    private static async Task<GitProcessResult> RunGitAsync(
        string workspace,
        IReadOnlyList<string> arguments,
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
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new AgentExecutionException("git process could not be started");
        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        string error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new GitProcessResult(process.ExitCode, output, error);
    }

    private static bool IsEnvironmentVariable(string value) =>
        value.All(character => char.IsLetterOrDigit(character) || character == '_');

    private sealed record GitProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);

}

public sealed class DockerAgentWorkspacePreparer : IAgentWorkspacePreparer
{
    public async Task PrepareAsync(
        AgentExecutionRequest request,
        CancellationToken cancellationToken)
    {
        request.Validate();
        string containerName = request.ContainerName ?? $"agent-{Guid.NewGuid():N}";
        bool created = false;
        try
        {
            DockerProcessResult creation = await DockerAgentProcess.RunAsync(
                BuildContainerCreateArguments(request, containerName),
                credentialVariable: null,
                credentialValue: null,
                cancellationToken);
            EnsureSuccessful(creation, "agent container creation");
            created = true;

            DockerProcessResult start = await DockerAgentProcess.RunAsync(
                ["start", containerName],
                credentialVariable: null,
                credentialValue: null,
                cancellationToken);
            EnsureSuccessful(start, "agent container start");

            DockerProcessResult initialization = await DockerAgentProcess.RunAsync(
                BuildWorkspaceInitializationArguments(request, containerName),
                credentialVariable: null,
                credentialValue: null,
                cancellationToken);
            EnsureSuccessful(initialization, "agent workspace initialization");

            DockerProcessResult setup = await DockerAgentProcess.RunAsync(
                BuildWorkspaceSetupArguments(request, containerName),
                credentialVariable: null,
                credentialValue: null,
                cancellationToken);
            EnsureSuccessful(setup, "agent workspace setup");
        }
        catch
        {
            if (created)
            {
                await DockerAgentProcess.RunAsync(
                    ["rm", "--force", containerName],
                    credentialVariable: null,
                    credentialValue: null,
                    CancellationToken.None);
            }
            throw;
        }
    }

    public async Task PrepareForHostCleanupAsync(
        AgentExecutionRequest request,
        CancellationToken cancellationToken)
    {
        request.Validate();
        await Task.CompletedTask;
    }

    internal static IReadOnlyList<string> BuildContainerCreateArguments(
        AgentExecutionRequest request,
        string containerName) =>
        [
            "create",
            "--name", containerName,
            "--label", "com.bartr.wharf.managed=true",
            "--network", request.Network,
            "--memory", "8g",
            "--pids-limit", "1024",
            "--env", "HOME=/home/wharf",
            "--env", "XDG_CACHE_HOME=/home/wharf/.cache",
            "--env", "NPM_CONFIG_MANAGE_PACKAGE_MANAGER_VERSIONS=false",
            request.AgentImage,
            "/bin/bash",
            "-lc",
            "exec sleep infinity"
        ];

    internal static IReadOnlyList<string> BuildWorkspaceInitializationArguments(
        AgentExecutionRequest request,
        string containerName) =>
        [
            "exec",
            "--user", "0:0",
            containerName,
            "/bin/bash",
            "-lc",
            "rm -rf /workspace /home/wharf /logs/agent && " +
            "mkdir -p /workspace /home/wharf/.cache /logs/agent && " +
            "cp -a /app/. /workspace/ && " +
            "chown -R 1000:1000 /workspace /home/wharf /logs/agent"
        ];

    internal static IReadOnlyList<string> BuildWorkspaceSetupArguments(
        AgentExecutionRequest request,
        string containerName) =>
        [
            "exec",
            "--user", "1000:1000",
            "--env", "HOME=/home/wharf",
            "--env", "XDG_CACHE_HOME=/home/wharf/.cache",
            containerName,
            "/bin/bash",
            "-lc",
            "if ! command -v node >/dev/null 2>&1; then " +
            "echo 'Cached Copilot CLI requires Node.js 22+.' >&2; exit 127; fi; " +
            "package=/opt/wharf/copilot/node_modules/@github/copilot/package.json; " +
            "if [ ! -f \"$package\" ]; then " +
            "echo 'Cached Copilot CLI package is missing.' >&2; exit 127; fi; " +
            "actual=$(node -p \"require('$package').version\") || exit 127; " +
            $"if [ \"$actual\" != \"{request.Invocation.EffectiveVersion}\" ]; then " +
            $"echo 'Cached Copilot CLI version mismatch: expected " +
            $"{request.Invocation.EffectiveVersion}, found '\"$actual\" >&2; exit 127; fi"
        ];

    internal static void EnsureSuccessful(DockerProcessResult result, string stage)
    {
        if (result.ExitCode == 0)
        {
            return;
        }

        string diagnostic = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();
        throw new AgentExecutionException(
            string.IsNullOrWhiteSpace(diagnostic)
                ? $"{stage} failed with exit code {result.ExitCode}"
                : $"{stage} failed: {diagnostic}");
    }
}

internal static class DockerAgentProcess
{
    public static async Task<DockerProcessResult> RunAsync(
        IReadOnlyList<string> arguments,
        string? credentialVariable,
        string? credentialValue,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (credentialVariable is not null && credentialValue is not null)
        {
            startInfo.Environment[credentialVariable] = credentialValue;
        }
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new AgentExecutionException("Docker agent process could not be started");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new DockerProcessResult(
            process.ExitCode,
            await outputTask,
            await errorTask);
    }
}

internal sealed record DockerProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

internal interface IDockerOomMonitor
{
    IDockerOomWatch Start(string containerName);
}

internal interface IDockerOomWatch : IAsyncDisposable
{
    Task<bool> WaitAsync(Task containerCompletion, CancellationToken cancellationToken);
}

internal sealed class DockerEventOomMonitor : IDockerOomMonitor
{
    public IDockerOomWatch Start(string containerName) =>
        new DockerEventOomWatch(containerName);
}

internal sealed class DockerEventOomWatch : IDockerOomWatch
{
    private readonly string containerName;
    private readonly Process process;

    public DockerEventOomWatch(string containerName)
    {
        this.containerName = containerName;
        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("events");
        startInfo.ArgumentList.Add("--filter");
        startInfo.ArgumentList.Add($"container={containerName}");
        startInfo.ArgumentList.Add("--filter");
        startInfo.ArgumentList.Add("event=oom");
        startInfo.ArgumentList.Add("--filter");
        startInfo.ArgumentList.Add("event=die");
        startInfo.ArgumentList.Add("--format");
        startInfo.ArgumentList.Add("{{.Action}}");
        process = Process.Start(startInfo)
            ?? throw new AgentExecutionException("Docker OOM monitor could not be started");
    }

    public async Task<bool> WaitAsync(
        Task containerCompletion,
        CancellationToken cancellationToken)
    {
        while (!containerCompletion.IsCompleted)
        {
            Task<string?> eventTask = process.StandardOutput.ReadLineAsync(cancellationToken).AsTask();
            Task completed = await Task.WhenAny(eventTask, containerCompletion);
            if (completed == containerCompletion)
            {
                completed = await Task.WhenAny(
                    eventTask,
                    Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken));
                if (completed != eventTask)
                {
                    return false;
                }
            }

            string? action = await eventTask;
            if (action is null || action.Equals("die", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (action.Equals("oom", StringComparison.OrdinalIgnoreCase))
            {
                await DockerContainerControl.StopAsync(containerName, cancellationToken);
                return true;
            }
        }

        return false;
    }

    public async ValueTask DisposeAsync()
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
        }

        process.Dispose();
    }
}

internal static class DockerContainerControl
{
    public static async Task StopAsync(
        string containerName,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("stop");
        startInfo.ArgumentList.Add("--time");
        startInfo.ArgumentList.Add("2");
        startInfo.ArgumentList.Add(containerName);
        using Process stop = Process.Start(startInfo)
            ?? throw new AgentExecutionException("Docker OOM stop process could not be started");
        Task<string> outputTask = stop.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = stop.StandardError.ReadToEndAsync(cancellationToken);
        await stop.WaitForExitAsync(cancellationToken);
        _ = await outputTask;
        string error = await errorTask;
        if (stop.ExitCode != 0 &&
            !error.Contains("No such container", StringComparison.OrdinalIgnoreCase))
        {
            throw new AgentExecutionException(
                $"failed to stop agent container {containerName}: {error.Trim()}");
        }
    }
}
