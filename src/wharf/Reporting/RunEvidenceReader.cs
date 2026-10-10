using System.Text.Json;
using Wharf.Agent;
using Wharf.State;
using Wharf.Tasks;

namespace Wharf.Reporting;

public sealed class RunEvidenceReader
{
    public IReadOnlyList<AnalyticsTrial> Read(
        string runDirectory,
        AnalyticsExportOptions options)
    {
        Validate(runDirectory, options);
        string fullRunDirectory = Path.GetFullPath(runDirectory);
        IReadOnlyDictionary<string, HarborTask> tasks = LoadTasks(fullRunDirectory);
        IReadOnlyList<StageEvidence> stages = ReadStages(
            Path.Combine(fullRunDirectory, "run.jsonl"));
        string job = options.Job ?? Path.GetFileName(fullRunDirectory);

        var rows = new List<AnalyticsTrial>();
        foreach (string trialDirectory in Directory.EnumerateDirectories(fullRunDirectory)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            string resultPath = Path.Combine(trialDirectory, "result.json");
            if (!File.Exists(resultPath))
            {
                continue;
            }

            using JsonDocument result = ReadJson(resultPath);
            JsonElement root = result.RootElement;
            string taskId = RequiredString(root, "taskId", resultPath);
            int attempt = RequiredInt(root, "attempt", resultPath);
            tasks.TryGetValue(taskId, out HarborTask? task);
            IReadOnlyList<AgentEvidenceSummary> agentEvidence = ReadAgentEvidence(trialDirectory);
            RewardEvidence reward = ReadReward(trialDirectory, root);
            decimal? credits = root.TryGetProperty("totalCost", out _)
                ? OptionalDecimal(root, "totalCost")
                : SumNullable(agentEvidence.Select(evidence => evidence.Cost));
            AgentTotals totals = SumAgentEvidence(agentEvidence);
            IReadOnlyList<StageEvidence> trialStages = stages
                .Where(stage =>
                    stage.TaskId == taskId &&
                    stage.LogicalAttempt == attempt)
                .ToArray();
            double? trialSeconds = ElapsedSeconds(trialStages);
            string? exceptionType = trialStages
                .Where(stage => !string.IsNullOrWhiteSpace(stage.ErrorType))
                .OrderBy(stage => stage.Timestamp)
                .LastOrDefault()?.ErrorType;
            string patchPath = Path.Combine(trialDirectory, "artifacts", "model.patch");

            rows.Add(new AnalyticsTrial(
                options.Arm,
                job,
                Path.GetFileName(trialDirectory),
                taskId,
                OptionalString(root, "language") ?? task?.Language,
                options.Agent,
                options.AgentVersion,
                options.Model,
                options.Effort,
                options.Subagents,
                reward.Reward,
                reward.FunctionalPassed,
                reward.FunctionalTotal,
                reward.RegressionPassed,
                reward.RegressionTotal,
                credits / 100m,
                credits,
                totals.Requests,
                totals.InputTokens,
                totals.CacheTokens,
                totals.OutputTokens,
                totals.Steps,
                null,
                totals.DurationMilliseconds is null
                    ? null
                    : Math.Round(totals.DurationMilliseconds.Value / 1000d, 3),
                trialSeconds,
                File.Exists(patchPath) ? new FileInfo(patchPath).Length : null,
                exceptionType ?? AgentException(agentEvidence) ??
                    (OptionalString(root, "verifierError") is null ? null : "Verifier"),
                OptionalString(root, "taskChecksum") ?? task?.SourceChecksum));
        }

        return rows;
    }

    private static IReadOnlyDictionary<string, HarborTask> LoadTasks(string runDirectory)
    {
        string? resultPath = Directory.EnumerateFiles(
                runDirectory, "result.json", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();
        if (resultPath is null)
        {
            resultPath = Directory.EnumerateDirectories(runDirectory)
                .Select(directory => Path.Combine(directory, "result.json"))
                .FirstOrDefault(File.Exists);
        }
        if (resultPath is null)
        {
            return new Dictionary<string, HarborTask>(StringComparer.Ordinal);
        }

        try
        {
            using JsonDocument result = ReadJson(resultPath);
            string? taskRoot = OptionalString(result.RootElement, "taskRoot");
            if (taskRoot is null || !Directory.Exists(taskRoot))
            {
                return new Dictionary<string, HarborTask>(StringComparer.Ordinal);
            }
            return new HarborTaskLoader().Load(taskRoot)
                .ToDictionary(task => task.TaskId, StringComparer.Ordinal);
        }
        catch (HarborTaskException)
        {
            return new Dictionary<string, HarborTask>(StringComparer.Ordinal);
        }
    }

    private static IReadOnlyList<AgentEvidenceSummary> ReadAgentEvidence(string trialDirectory)
    {
        var summaries = new List<AgentEvidenceSummary>();
        var agentDirectories = new List<string>();
        string currentAgent = Path.Combine(trialDirectory, "agent");
        if (Directory.Exists(currentAgent))
        {
            agentDirectories.Add(currentAgent);
        }
        string trialRetries = Path.Combine(trialDirectory, "trial-retries");
        if (Directory.Exists(trialRetries))
        {
            agentDirectories.AddRange(Directory.EnumerateDirectories(trialRetries)
                .Select(retry => Path.Combine(retry, "agent"))
                .Where(Directory.Exists));
        }

        foreach (string path in agentDirectories
                     .SelectMany(AgentResultPaths)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            try
            {
                AgentEvidenceSummary? summary = JsonSerializer.Deserialize<AgentEvidenceSummary>(
                    File.ReadAllText(path), StateJson.Options);
                if (summary is not null)
                {
                    summaries.Add(summary);
                }
            }
            catch (JsonException exception)
            {
                throw new AnalyticsEvidenceException(
                    $"invalid agent evidence '{path}': {exception.Message}");
            }
        }
        return summaries;
    }

    private static IEnumerable<string> AgentResultPaths(string agentDirectory)
    {
        string current = Path.Combine(agentDirectory, "result.json");
        if (File.Exists(current))
        {
            yield return current;
        }

        string retries = Path.Combine(agentDirectory, "infrastructure-retries");
        if (!Directory.Exists(retries))
        {
            yield break;
        }
        foreach (string path in Directory.EnumerateDirectories(retries)
                     .Select(directory => Path.Combine(directory, "result.json"))
                     .Where(File.Exists))
        {
            yield return path;
        }
    }

    private static AgentTotals SumAgentEvidence(IReadOnlyList<AgentEvidenceSummary> evidence)
    {
        long? duration = SumNullable(evidence.Select(item => (long?)item.DurationMilliseconds));
        long? input = SumNullable(evidence.Select(item => item.Usage?.InputTokens));
        long? output = SumNullable(evidence.Select(item => item.Usage?.OutputTokens));
        long? cache = SumNullable(evidence.Select(item =>
            item.Usage is null
                ? (long?)null
                : item.Usage.CacheReadTokens + item.Usage.CacheWriteTokens));
        int? requests = SumNullable(evidence.Select(item =>
            item.Usage?.Requests ?? item.Requests));
        int? steps = SumNullable(evidence.Select(item => item.Steps));
        return new AgentTotals(duration, input, output, cache, requests, steps);
    }

    private static RewardEvidence ReadReward(string trialDirectory, JsonElement result)
    {
        string path = Path.Combine(trialDirectory, "verifier", "reward.json");
        if (!File.Exists(path))
        {
            return new RewardEvidence(
                OptionalString(result, "status") == "passed" ? 1 : 0,
                null, null, null, null);
        }

        using JsonDocument reward = ReadJson(path);
        JsonElement root = reward.RootElement;
        return new RewardEvidence(
            OptionalDouble(root, "reward") ?? 0,
            OptionalInt(root, "f2p_passed"),
            OptionalInt(root, "f2p_total"),
            OptionalInt(root, "p2p_passed"),
            OptionalInt(root, "p2p_total"));
    }

    private static IReadOnlyList<StageEvidence> ReadStages(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var stages = new List<StageEvidence>();
        int lineNumber = 0;
        foreach (string line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            try
            {
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement root = document.RootElement;
                if (OptionalString(root, "eventType") != "stage")
                {
                    continue;
                }
                stages.Add(new StageEvidence(
                    DateTimeOffset.Parse(
                        RequiredString(root, "timestamp", path),
                        System.Globalization.CultureInfo.InvariantCulture),
                    RequiredString(root, "taskId", path),
                    RequiredInt(root, "logicalAttempt", path),
                    OptionalString(root, "errorType")));
            }
            catch (Exception exception) when (
                exception is JsonException or FormatException or AnalyticsEvidenceException)
            {
                throw new AnalyticsEvidenceException(
                    $"invalid stage evidence '{path}' line {lineNumber}: {exception.Message}");
            }
        }
        return stages;
    }

    private static double? ElapsedSeconds(IReadOnlyList<StageEvidence> stages)
    {
        if (stages.Count == 0)
        {
            return null;
        }
        return Math.Round(
            (stages.Max(stage => stage.Timestamp) - stages.Min(stage => stage.Timestamp))
                .TotalSeconds,
            3);
    }

    private static string? AgentException(IEnumerable<AgentEvidenceSummary> evidence) =>
        evidence.Where(item => !string.IsNullOrWhiteSpace(item.ErrorType))
            .LastOrDefault()?.ErrorType;

    private static JsonDocument ReadJson(string path)
    {
        try
        {
            return JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            throw new AnalyticsEvidenceException(
                $"invalid JSON evidence '{path}': {exception.Message}");
        }
    }

    private static string RequiredString(JsonElement root, string name, string path) =>
        OptionalString(root, name) ??
        throw new AnalyticsEvidenceException($"evidence '{path}' is missing {name}");

    private static int RequiredInt(JsonElement root, string name, string path) =>
        OptionalInt(root, name) ??
        throw new AnalyticsEvidenceException($"evidence '{path}' is missing {name}");

    private static string? OptionalString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? OptionalInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out int parsed)
            ? parsed
            : null;

    private static double? OptionalDouble(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out double parsed)
            ? parsed
            : null;

    private static decimal? OptionalDecimal(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDecimal(out decimal parsed)
            ? parsed
            : null;

    private static T? SumNullable<T>(IEnumerable<T?> values)
        where T : struct, System.Numerics.INumber<T>
    {
        T total = T.Zero;
        bool found = false;
        foreach (T? value in values)
        {
            if (!value.HasValue)
            {
                continue;
            }
            total += value.Value;
            found = true;
        }
        return found ? total : null;
    }

    private static void Validate(string runDirectory, AnalyticsExportOptions options)
    {
        if (string.IsNullOrWhiteSpace(runDirectory) || !Directory.Exists(runDirectory))
        {
            throw new AnalyticsEvidenceException(
                $"run evidence directory does not exist: {runDirectory}");
        }
        if (string.IsNullOrWhiteSpace(options.Arm) ||
            string.IsNullOrWhiteSpace(options.Agent) ||
            string.IsNullOrWhiteSpace(options.Model) ||
            string.IsNullOrWhiteSpace(options.Effort))
        {
            throw new AnalyticsEvidenceException(
                "analytics arm, agent, model, and effort are required");
        }
    }

    private sealed record StageEvidence(
        DateTimeOffset Timestamp,
        string TaskId,
        int LogicalAttempt,
        string? ErrorType);

    private sealed record RewardEvidence(
        double? Reward,
        int? FunctionalPassed,
        int? FunctionalTotal,
        int? RegressionPassed,
        int? RegressionTotal);

    private sealed record AgentTotals(
        long? DurationMilliseconds,
        long? InputTokens,
        long? OutputTokens,
        long? CacheTokens,
        int? Requests,
        int? Steps);
}
