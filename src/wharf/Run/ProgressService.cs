using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Wharf.Run;

public sealed record RunProgress(
    int TotalRecords,
    int TestsRunning,
    int Building,
    int Agents,
    int Verifiers,
    int Cleanup,
    int TestsCompleted,
    int FailedTests,
    int Timeouts,
    int Errors,
    IReadOnlyDictionary<string, int> ErrorTypes,
    int PassAtTotal,
    decimal? PassPercent,
    IReadOnlyDictionary<int, ProgressGroup> ByK,
    decimal? AverageCost,
    decimal? TotalCost,
    IReadOnlyList<ActiveTrialProgress> ActiveTrials,
    int Retries = 0,
    int Queued = 0,
    int MissingGraderResults = 0,
    IReadOnlyDictionary<string, int>? MissingGraderTypes = null,
    IReadOnlyList<PreparationWaitAggregate>? PreparationWaits = null);

public sealed record ActiveTrialProgress(
    string Name,
    string Stage,
    long DurationMilliseconds);

public sealed record ProgressGroup(
    int TestsCompleted,
    int FailedTests,
    int Timeouts,
    int Errors,
    int PassAtTotal,
    decimal? PassPercent,
    decimal? AverageCost,
    decimal? TotalCost);

public sealed record PreparationWaitAggregate(
    string Operation,
    int K,
    int Count,
    long TotalWaitMilliseconds,
    decimal AverageWaitMilliseconds,
    long MaximumWaitMilliseconds);

public static class ProgressService
{
    private const decimal UsdPerAiCredit = 0.01m;

    private static readonly Regex RunPattern = new(
        @"(?:run=(?<run>\d+)/\d+.*?)?\btask=(?<task>\S+)(?:.*?\battempt=(?<attempt>\d+)/\d+)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex StatusPattern = new(
        @"\bstatus=(?<status>\S+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ResultPattern = new(
        @"\bresult=(?<result>\S+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ErrorPattern = new(
        @"\berror=(?<error>.+?)(?=\s+\w+=|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CostPattern = new(
        @"\bcost=(?<cost>[0-9]+(?:\.[0-9]+)?)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static RunProgress Read(string path, DateTimeOffset? now = null)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"run log does not exist: {path}", path);

        var records = new Dictionary<string, ProgressRecord>(StringComparer.Ordinal);
        var errorTypes = new Dictionary<string, int>(StringComparer.Ordinal);
        var fullTaskNames = new Dictionary<int, string>();
        var queuedOperations = new Dictionary<string, PreparationQueueRecord>(
            StringComparer.Ordinal);
        var preparationWaits = new List<PreparationQueueRecord>();
        int totalRecords = 0;
        int retries = 0;

        foreach (string line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            LogRecord? log;
            try
            {
                log = JsonSerializer.Deserialize<LogRecord>(line, JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (log is null)
                continue;
            totalRecords++;
            if (log.Message.StartsWith("resume plan ", StringComparison.Ordinal))
            {
                foreach (string staleKey in records
                             .Where(item => !item.Value.Terminal)
                             .Select(item => item.Key)
                             .ToArray())
                {
                    records.Remove(staleKey);
                }
                fullTaskNames.Clear();
                queuedOperations.Clear();
                continue;
            }
            if (log.EventType == "preparation-gate" &&
                TryPreparationQueueRecord(log, out PreparationQueueRecord queueRecord))
            {
                if (log.Transition == "waiting")
                {
                    queuedOperations[queueRecord.Key] = queueRecord;
                }
                else if (log.Transition is "acquired" or "cancelled")
                {
                    queuedOperations.Remove(queueRecord.Key);
                    if (log.WaitMilliseconds is not null)
                        preparationWaits.Add(queueRecord);
                }
                else if (log.Transition == "released")
                {
                    queuedOperations.Remove(queueRecord.Key);
                }
                continue;
            }
            if (log.EventType == "stage" &&
                log.RunNumber is int stageRunNumber &&
                !string.IsNullOrWhiteSpace(log.TaskId))
            {
                fullTaskNames[stageRunNumber] = log.TaskId;
                continue;
            }
            Match match = RunPattern.Match(log.Message);
            if (!match.Success)
                continue;

            string key =
                $"{match.Groups["run"].Value}:{match.Groups["task"].Value}:" +
                $"{match.Groups["attempt"].Value}";
            int runNumber = match.Groups["run"].Success
                ? int.Parse(match.Groups["run"].Value, CultureInfo.InvariantCulture)
                : 1;
            int k = match.Groups["attempt"].Success
                ? int.Parse(match.Groups["attempt"].Value, CultureInfo.InvariantCulture)
                : 1;
            ProgressRecord record = records.TryGetValue(key, out ProgressRecord? existing)
                ? existing with { K = k }
                : new ProgressRecord(k, runNumber, match.Groups["task"].Value);
            Match status = StatusPattern.Match(log.Message);
            if (status.Success)
            {
                string currentStatus = status.Groups["status"].Value;
                bool isRetry = IsRetry(currentStatus);
                if (isRetry)
                    retries++;
                string? stage = StageForStatus(currentStatus);
                DateTimeOffset? observedAt = DateTimeOffset.TryParse(
                    log.Timestamp,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTimeOffset parsedTimestamp)
                    ? parsedTimestamp
                    : null;
                record = record with
                {
                    Status = currentStatus,
                    Stage = stage ?? record.Stage,
                    StageStartedAt = stage is not null && stage != record.Stage
                        ? observedAt
                        : record.StageStartedAt,
                    Failed = isRetry
                        ? false
                        : record.Failed ||
                            currentStatus.Contains("failed", StringComparison.Ordinal),
                    Terminal = isRetry
                        ? false
                        : record.Terminal || currentStatus == "complete",
                    HasGraderResult = isRetry
                        ? false
                        : record.HasGraderResult || currentStatus == "verifier-complete",
                    Result = isRetry ? null : record.Result,
                    ErrorType = isRetry ? null : record.ErrorType
                };
            }

            Match result = ResultPattern.Match(log.Message);
            if (result.Success)
            {
                bool succeeded = result.Groups["result"].Value is "passed" or "completed";
                bool isTerminal = record.Status == "complete";
                record = record with
                {
                    Result = result.Groups["result"].Value,
                    Failed = !succeeded,
                    ErrorType = isTerminal && succeeded
                        ? null
                        : record.ErrorType
                };
            }

            if (!string.IsNullOrWhiteSpace(log.Error) || !string.IsNullOrWhiteSpace(log.ErrorType))
            {
                record = record with
                {
                    Failed = true,
                    ErrorType = record.ErrorType ?? log.ErrorType ?? "Run"
                };
            }

            Match cost = CostPattern.Match(log.Message);
            if (cost.Success &&
                decimal.TryParse(cost.Groups["cost"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
            {
                record = record with { Cost = value * UsdPerAiCredit };
            }

            if (status.Success && status.Groups["status"].Value.Contains("failed", StringComparison.Ordinal))
            {
                record = record with
                {
                    Failed = true,
                    ErrorType = record.ErrorType ?? ErrorTypeFromMessage(log.Message)
                };
            }

            records[key] = record;
        }

        HashSet<int> queuedRuns = queuedOperations.Values
            .Select(record => record.RunNumber)
            .ToHashSet();
        IEnumerable<ProgressRecord> active = records.Values.Where(record =>
            !record.Terminal &&
            record.Status is not null &&
            !queuedRuns.Contains(record.RunNumber));
        IEnumerable<ProgressRecord> terminal = records.Values.Where(record => record.Terminal);
        IEnumerable<ProgressRecord> completed = terminal.Where(record => !record.Failed);
        IEnumerable<ProgressRecord> failed = terminal.Where(record => record.Failed);
        List<ProgressRecord> failedTests = failed
            .Where(record =>
                record.ErrorType is null or "Verifier" &&
                !IsTimeout(record))
            .ToList();
        List<ProgressRecord> timeouts = failed
            .Where(IsTimeout)
            .ToList();
        List<ProgressRecord> errors = failed
            .Where(record =>
                record.ErrorType is not (null or "Verifier") &&
                !IsTimeout(record))
            .ToList();
        foreach (ProgressRecord record in errors)
        {
            string type = record.ErrorType ?? "Run";
            errorTypes[type] = errorTypes.TryGetValue(type, out int count) ? count + 1 : 1;
        }
        var groups = records.Values
            .GroupBy(record => record.K)
            .ToDictionary(
                group => group.Key,
                group => ToGroup(group.Where(record => record.Terminal)),
                EqualityComparer<int>.Default);
        List<ProgressRecord> missingGraders = terminal
            .Where(record => !record.HasGraderResult)
            .ToList();
        IReadOnlyDictionary<string, int> missingGraderTypes = missingGraders
            .GroupBy(
                record => record.ErrorType ?? record.Result ?? "Unknown",
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Count(),
                StringComparer.Ordinal);
        IReadOnlyList<PreparationWaitAggregate> waitAggregates = preparationWaits
            .GroupBy(record => (record.Operation, record.K))
            .OrderBy(group => group.Key.Operation, StringComparer.Ordinal)
            .ThenBy(group => group.Key.K)
            .Select(group => new PreparationWaitAggregate(
                group.Key.Operation,
                group.Key.K,
                group.Count(),
                group.Sum(record => record.WaitMilliseconds),
                group.Average(record => (decimal)record.WaitMilliseconds),
                group.Max(record => record.WaitMilliseconds)))
            .ToList();
        List<decimal> costs = completed
            .Where(record => record.Cost.HasValue)
            .Select(record => record.Cost!.Value)
            .ToList();
        DateTimeOffset currentTime = now ?? DateTimeOffset.UtcNow;
        List<ActiveTrialProgress> activeTrials = active
            .Select(record => new ActiveTrialProgress(
                fullTaskNames.TryGetValue(record.RunNumber, out string? fullName)
                    ? fullName
                    : record.Name,
                record.Stage ?? StageForStatus(record.Status) ?? "unknown",
                record.StageStartedAt is null
                    ? 0
                    : Math.Max(
                        0,
                        (long)(currentTime - record.StageStartedAt.Value).TotalMilliseconds)))
            .OrderByDescending(record => record.DurationMilliseconds)
            .ThenBy(record => record.Name, StringComparer.Ordinal)
            .ToList();

        return new RunProgress(
            totalRecords,
            active.Count(),
            active.Count(record => IsBuilding(record.Status)),
            active.Count(record => IsAgent(record.Status)),
            active.Count(record => IsVerifier(record.Status)),
            active.Count(record => IsCleanup(record.Status)),
            terminal.Count(),
            failedTests.Count,
            timeouts.Count,
            errors.Count,
            errorTypes,
            completed.Count(record =>
                record.HasGraderResult &&
                string.Equals(record.Result, "passed", StringComparison.OrdinalIgnoreCase)),
            Percentage(completed.Count(record =>
                record.HasGraderResult &&
                string.Equals(record.Result, "passed", StringComparison.OrdinalIgnoreCase)), terminal.Count()),
            groups,
            costs.Count == 0 ? null : costs.Average(),
            costs.Count == 0 ? null : costs.Sum(),
            activeTrials,
            retries,
            queuedOperations.Count,
            missingGraders.Count,
            missingGraderTypes,
            waitAggregates);
    }

    private static bool IsBuilding(string? status) =>
        status is "trial-retry" or
            "waiting-preparation" or
            "preparing-image" or
            "agent" or
            "image-ready" or
            "preparing-workspace" or
            "workspace-ready";

    private static bool IsAgent(string? status) =>
        status is "running-agent" or
            "agent-infrastructure-retry" or
            "agent-complete";

    private static bool IsVerifier(string? status) =>
        status is "verifier-retry" or
            "running-verifier" or
            "preparing-verifier-image" or
            "verifier-image-ready" or
            "verifier-complete";

    private static bool IsRetry(string? status) =>
        status is "trial-retry" or
            "agent-infrastructure-retry" or
            "verifier-retry";

    private static bool IsCleanup(string? status) =>
        status is "verifier-image-cleaned" or
            "cleaning-up" or
            "workspace-cleanup-failed";

    private static string? StageForStatus(string? status)
    {
        if (IsBuilding(status))
            return "building";
        if (IsAgent(status))
            return "agent";
        if (IsVerifier(status))
            return "verifier";
        if (IsCleanup(status))
            return "cleanup";
        return null;
    }

    private static ProgressGroup ToGroup(IEnumerable<ProgressRecord> records)
    {
        List<ProgressRecord> terminal = records.ToList();
        List<ProgressRecord> completed = terminal
            .Where(record => !record.Failed)
            .ToList();
        int failedTests = terminal.Count(record =>
            record.Failed &&
            record.ErrorType is null or "Verifier" &&
            !IsTimeout(record));
        int timeouts = terminal.Count(IsTimeout);
        int errors = terminal.Count(record =>
            record.Failed &&
            record.ErrorType is not (null or "Verifier") &&
            !IsTimeout(record));
        List<decimal> costs = completed
            .Where(record => record.Cost.HasValue)
            .Select(record => record.Cost!.Value)
            .ToList();
        return new ProgressGroup(
            terminal.Count,
            failedTests,
            timeouts,
            errors,
            completed.Count(record =>
                record.HasGraderResult &&
                string.Equals(record.Result, "passed", StringComparison.OrdinalIgnoreCase)),
            Percentage(completed.Count(record =>
                record.HasGraderResult &&
                string.Equals(record.Result, "passed", StringComparison.OrdinalIgnoreCase)), terminal.Count),
            costs.Count == 0 ? null : costs.Average(),
            costs.Count == 0 ? null : costs.Sum());
    }

    private static bool IsTimeout(ProgressRecord record) =>
        record.Failed &&
        record.ErrorType?.Contains("Timeout", StringComparison.Ordinal) == true;

    private static decimal? Percentage(int numerator, int denominator) =>
        denominator == 0 ? null : numerator * 100m / denominator;

    private static string ErrorTypeFromMessage(string message)
    {
        Match match = ErrorPattern.Match(message);
        if (!match.Success)
            return "Run";
        string error = match.Groups["error"].Value.Trim();
        int separator = error.IndexOf(':');
        return separator > 0 ? error[..separator] : error.Split(' ', 2)[0];
    }

    private sealed record LogRecord(
        string Timestamp,
        string Level,
        string Message,
        string? Error = null,
        string? ErrorType = null,
        string? EventType = null,
        int? RunNumber = null,
        string? TaskId = null,
        string? Operation = null,
        string? Transition = null,
        int? LogicalAttempt = null,
        int? TrialRetry = null,
        int? OperationRetry = null,
        int? K = null,
        long? WaitMilliseconds = null);
    private sealed record ProgressRecord(
        int K,
        int RunNumber,
        string Name,
        string? Status = null,
        string? Result = null,
        decimal? Cost = null,
        bool Terminal = false,
        bool HasGraderResult = false,
        bool Failed = false,
        string? ErrorType = null,
        string? Stage = null,
        DateTimeOffset? StageStartedAt = null);

    private sealed record PreparationQueueRecord(
        string Key,
        int RunNumber,
        string Operation,
        int K,
        long WaitMilliseconds);

    private static bool TryPreparationQueueRecord(
        LogRecord log,
        out PreparationQueueRecord record)
    {
        if (log.RunNumber is not int runNumber ||
            string.IsNullOrWhiteSpace(log.TaskId) ||
            string.IsNullOrWhiteSpace(log.Operation) ||
            log.LogicalAttempt is not int logicalAttempt ||
            log.TrialRetry is not int trialRetry ||
            log.K is not int k)
        {
            record = null!;
            return false;
        }

        int operationRetry = log.OperationRetry ?? 0;
        string key =
            $"{runNumber}:{log.TaskId}:{logicalAttempt}:{trialRetry}:" +
            $"{log.Operation}:{operationRetry}";
        record = new PreparationQueueRecord(
            key,
            runNumber,
            log.Operation,
            k,
            Math.Max(0, log.WaitMilliseconds ?? 0));
        return true;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
