using System.Text.Json;
using System.Text.RegularExpressions;
using Wharf.Configuration;
using Wharf.Tasks;

namespace Wharf.Recovery;

public sealed record RetainedResumeAttempt(
    TaskAttempt Attempt,
    string TrialDirectory,
    int TrialRetry,
    decimal? TotalCost,
    bool HasInvalidTerminalResult = false);

public sealed record RetainedResumePlan(
    string RunDirectory,
    string TaskRoot,
    int K,
    int N,
    int P,
    string Model,
    string Effort,
    string CliVersion,
    bool ShortestFirst,
    IReadOnlyList<RetainedResumeAttempt> Attempts,
    int CompletedCount,
    RunConfiguration Configuration);

public sealed class RetainedRunResumePlanner
{
    private static readonly Regex ArmPattern = new(
        @"^copilot-(?<model>.+)-(?<effort>default|low|medium|high|max)-(?<version>\d+\.\d+\.\d+)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex PlanPattern = new(
        @"plan tasks=(?<tasks>\d+) attempts=(?<k>\d+) runs=(?<runs>\d+) " +
        @"task-concurrency=(?<n>\d+) preparation-concurrency=(?<p>\d+) " +
        @"admission-order=(?<order>longest-first|shortest-first)",
        RegexOptions.CultureInvariant);

    public RetainedResumePlan Plan(
        string runDirectory,
        string? taskRoot = null,
        int? n = null,
        int? p = null)
    {
        string resolvedRun = Path.GetFullPath(runDirectory);
        if (!Directory.Exists(resolvedRun))
            throw new RecoveryException($"resume run directory does not exist: {resolvedRun}");

        string logPath = Path.Combine(resolvedRun, "run.jsonl");
        if (!File.Exists(logPath))
            throw new RecoveryException($"resume run log does not exist: {logPath}");

        Match arm = ArmPattern.Match(
            Directory.GetParent(resolvedRun)?.Name ?? string.Empty);
        if (!arm.Success)
        {
            throw new RecoveryException(
                "resume run parent must use copilot-<model>-<effort>-<version>");
        }

        Match metadata = ReadPlanMetadata(logPath);
        int k = ParsePositive(metadata, "k", "attempt count");
        int taskCount = ParsePositive(metadata, "tasks", "task count");
        int plannedRuns = ParsePositive(metadata, "runs", "planned run count");
        int loggedTaskConcurrency = ParsePositive(metadata, "n", "task concurrency");
        int loggedPreparationConcurrency = ParsePositive(
            metadata,
            "p",
            "preparation concurrency");
        RetainedRunMetadata? retainedMetadata =
            RetainedRunMetadataStore.Read(resolvedRun);
        int retainedTaskConcurrency = retainedMetadata?.Configuration.N ??
            loggedTaskConcurrency;
        int retainedPreparationConcurrency = retainedMetadata?.Configuration.P ??
            loggedPreparationConcurrency;
        int taskConcurrency = n ?? retainedTaskConcurrency;
        int preparationConcurrency = p ?? retainedPreparationConcurrency;
        if (taskConcurrency <= 0)
            throw new RecoveryException("resume task concurrency must be positive");
        if (preparationConcurrency <= 0)
            throw new RecoveryException("resume preparation concurrency must be positive");
        preparationConcurrency = Math.Min(preparationConcurrency, taskConcurrency);
        if (plannedRuns != checked(taskCount * k))
            throw new RecoveryException("resume run plan has inconsistent task and attempt counts");

        string resolvedTaskRoot = Path.GetFullPath(taskRoot ??
            retainedMetadata?.TaskRoot ??
            ReadTaskRoot(resolvedRun));
        IReadOnlyList<HarborTask> availableTasks =
            new HarborTaskLoader().Load(resolvedTaskRoot);
        IReadOnlyList<HarborTask> tasks;
        if (retainedMetadata is null)
        {
            tasks = availableTasks;
            if (tasks.Count != taskCount)
            {
                throw new RecoveryException(
                    $"resume task count mismatch: retained={taskCount} current={tasks.Count}");
            }
        }
        else
        {
            if (retainedMetadata.Tasks.Count != taskCount ||
                retainedMetadata.Configuration.K != k ||
                retainedMetadata.Configuration.N != loggedTaskConcurrency ||
                retainedMetadata.Configuration.P != loggedPreparationConcurrency ||
                !string.Equals(
                    retainedMetadata.Configuration.Model,
                    arm.Groups["model"].Value,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    retainedMetadata.Configuration.Effort,
                    arm.Groups["effort"].Value,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    retainedMetadata.Configuration.CliVersion,
                    arm.Groups["version"].Value,
                    StringComparison.Ordinal) ||
                retainedMetadata.Configuration.ShortestFirst !=
                    (metadata.Groups["order"].Value == "shortest-first"))
            {
                throw new RecoveryException(
                    "resume run plan does not match retained log metadata");
            }

            var availableById = availableTasks.ToDictionary(
                task => task.TaskId,
                StringComparer.Ordinal);
            tasks = retainedMetadata.Tasks.Select(identity =>
            {
                if (!availableById.TryGetValue(identity.TaskId, out HarborTask? task))
                {
                    throw new RecoveryException(
                        $"resume task is missing from current task root: {identity.TaskId}");
                }

                if (!string.Equals(
                        task.SourceChecksum,
                        identity.SourceChecksum,
                        StringComparison.Ordinal))
                {
                    throw new RecoveryException(
                        $"resume task checksum mismatch for {identity.TaskId}: " +
                        $"retained={identity.SourceChecksum} current={task.SourceChecksum}");
                }

                return task;
            }).ToArray();
        }

        IReadOnlyList<TaskAttempt> expected = TaskSelector.ExpandAttempts(tasks, k);
        var attempts = new List<RetainedResumeAttempt>();
        int completed = 0;
        foreach (TaskAttempt attempt in expected)
        {
            string trialDirectory = Path.Combine(
                resolvedRun,
                TrialDirectoryName(attempt, k));
            ValidateRetainedTrialIdentity(resolvedRun, trialDirectory, attempt, k);
            string resultPath = Path.Combine(trialDirectory, "result.json");
            if (File.Exists(resultPath) &&
                IsValidTerminalResult(resultPath, attempt))
            {
                completed++;
                continue;
            }

            int trialRetry = ExistingTrialRetry(trialDirectory);
            decimal? totalCost = ExistingTotalCost(trialDirectory);
            attempts.Add(new RetainedResumeAttempt(
                attempt,
                trialDirectory,
                trialRetry,
                totalCost,
                File.Exists(resultPath)));
        }

        bool shortestFirst = metadata.Groups["order"].Value == "shortest-first";
        RunConfiguration configuration = retainedMetadata?.Configuration ??
            new RunConfiguration(
                k,
                taskConcurrency,
                preparationConcurrency,
                new PhaseTimeouts(),
                new RetryPolicy(MaxAttempts: 1),
                "text",
                "jobs",
                arm.Groups["model"].Value,
                arm.Groups["effort"].Value,
                arm.Groups["version"].Value,
                [],
                [],
                30,
                false,
                "default",
                new SecretSettings(),
                shortestFirst);
        configuration = configuration with
        {
            K = k,
            N = taskConcurrency,
            P = preparationConcurrency,
            Credentials = new SecretSettings(),
            ShortestFirst = shortestFirst
        };
        RunConfigurationValidator.Validate(null, configuration);
        return new RetainedResumePlan(
            resolvedRun,
            resolvedTaskRoot,
            k,
            taskConcurrency,
            preparationConcurrency,
            arm.Groups["model"].Value,
            arm.Groups["effort"].Value,
            arm.Groups["version"].Value,
            shortestFirst,
            TaskSelector.OrderAttemptsForAdmission(
                    attempts.Select(item => item.Attempt).ToArray(),
                    shortestFirst)
                .Select(attempt => attempts.Single(item => item.Attempt.AttemptId == attempt.AttemptId))
                .ToArray(),
            completed,
            configuration);
    }

    internal static string TrialDirectoryName(TaskAttempt attempt, int k) =>
        $"{Sanitize(attempt.Task.TaskId)}__{attempt.Task.SourceChecksum[..8]}" +
        (k > 1 ? $"__attempt-{attempt.AttemptNumber}" : string.Empty);

    private static Match ReadPlanMetadata(string logPath)
    {
        foreach (string line in File.ReadLines(logPath))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(line);
                string? message = document.RootElement
                    .GetProperty("message")
                    .GetString();
                Match match = PlanPattern.Match(message ?? string.Empty);
                if (match.Success)
                    return match;
            }
            catch (JsonException exception)
            {
                throw new RecoveryException(
                    $"resume run log contains malformed JSON: {exception.Message}");
            }
        }

        throw new RecoveryException("resume run log has no plan metadata");
    }

    private static string ReadTaskRoot(string runDirectory)
    {
        foreach (string resultPath in Directory.EnumerateFiles(
                     runDirectory,
                     "result.json",
                     SearchOption.AllDirectories))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(resultPath));
                if (document.RootElement.TryGetProperty("taskRoot", out JsonElement taskRoot) &&
                    !string.IsNullOrWhiteSpace(taskRoot.GetString()))
                {
                    return taskRoot.GetString()!;
                }
            }
            catch (JsonException exception)
            {
                throw new RecoveryException(
                    $"resume result contains malformed JSON: {resultPath}: {exception.Message}");
            }
        }

        throw new RecoveryException(
            "resume cannot infer the task root; specify --tasks");
    }

    private static int ExistingTrialRetry(string trialDirectory)
    {
        string retries = Path.Combine(trialDirectory, "trial-retries");
        if (!Directory.Exists(retries))
            return 0;

        return Directory.EnumerateDirectories(retries, "retry-*")
            .Select(path => int.TryParse(
                Path.GetFileName(path)["retry-".Length..],
                out int value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static decimal? ExistingTotalCost(string trialDirectory)
    {
        int retry = ExistingTrialRetry(trialDirectory);
        decimal? totalCost = null;
        if (retry > 0)
        {
            string resultPath = Path.Combine(
                trialDirectory,
                "trial-retries",
                $"retry-{retry}",
                "result.json");
            totalCost = ReadOptionalCost(resultPath, "totalCost", "resume retry result");
        }

        string agentDirectory = Path.Combine(trialDirectory, "agent");
        if (!Directory.Exists(agentDirectory))
            return totalCost;

        foreach (string resultPath in Directory.EnumerateFiles(
                     agentDirectory,
                     "result.json",
                     SearchOption.AllDirectories))
        {
            decimal? cost = ReadOptionalCost(resultPath, "cost", "resume agent result");
            if (cost.HasValue)
                totalCost = (totalCost ?? 0) + cost.Value;
        }

        return totalCost;
    }

    private static decimal? ReadOptionalCost(
        string path,
        string property,
        string description)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty(property, out JsonElement cost) &&
                cost.ValueKind == JsonValueKind.Number
                ? cost.GetDecimal()
                : null;
        }

        catch (JsonException exception)
        {
            throw new RecoveryException(
                $"{description} contains malformed JSON: {path}: {exception.Message}");
        }
    }

    private static bool IsValidTerminalResult(
        string path,
        TaskAttempt attempt)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("taskId", out JsonElement taskId) &&
                string.Equals(
                    taskId.GetString(),
                    attempt.Task.TaskId,
                    StringComparison.Ordinal) &&
                root.TryGetProperty("attempt", out JsonElement attemptNumber) &&
                attemptNumber.TryGetInt32(out int retainedAttempt) &&
                retainedAttempt == attempt.AttemptNumber &&
                root.TryGetProperty("status", out JsonElement status) &&
                status.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(status.GetString());
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void ValidateRetainedTrialIdentity(
        string runDirectory,
        string expectedDirectory,
        TaskAttempt attempt,
        int k)
    {
        string suffix = k > 1
            ? $"__attempt-{attempt.AttemptNumber}"
            : string.Empty;
        string prefix = $"{Sanitize(attempt.Task.TaskId)}__";
        string[] candidates = Directory.EnumerateDirectories(runDirectory)
            .Where(path =>
            {
                string name = Path.GetFileName(path);
                return name.StartsWith(prefix, StringComparison.Ordinal) &&
                    name.EndsWith(suffix, StringComparison.Ordinal);
            })
            .ToArray();
        if (candidates.Length == 0 ||
            candidates.Any(path => string.Equals(
                Path.GetFullPath(path),
                Path.GetFullPath(expectedDirectory),
                StringComparison.Ordinal)))
        {
            return;
        }

        throw new RecoveryException(
            $"resume task checksum mismatch for {attempt.AttemptId}: " +
            $"expected {Path.GetFileName(expectedDirectory)}, retained " +
            $"{string.Join(", ", candidates.Select(Path.GetFileName))}");
    }

    private static int ParsePositive(Match match, string group, string description)
    {
        if (!int.TryParse(match.Groups[group].Value, out int value) || value <= 0)
            throw new RecoveryException($"resume run plan has invalid {description}");
        return value;
    }

    private static string Sanitize(string value) =>
        new(value.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_'
                ? character
                : '_').ToArray());
}

public static class ResumeEvidenceArchiver
{
    public static string Archive(string trialDirectory)
    {
        Directory.CreateDirectory(trialDirectory);
        string history = Path.Combine(trialDirectory, "resume-history");
        Directory.CreateDirectory(history);
        int next = 1;
        while (Directory.Exists(Path.Combine(history, $"resume-{next}")))
            next++;

        string destination = Path.Combine(history, $"resume-{next}");
        Directory.CreateDirectory(destination);
        foreach (string entry in Directory.EnumerateFileSystemEntries(trialDirectory).ToArray())
        {
            string name = Path.GetFileName(entry);
            if (name is "resume-history" or "trial-retries" or "verifier-retries")
                continue;

            string target = Path.Combine(destination, name);
            if (Directory.Exists(entry))
                Directory.Move(entry, target);
            else
                File.Move(entry, target);
        }

        return destination;
    }
}
