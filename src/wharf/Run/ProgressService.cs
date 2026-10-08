using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Wharf.Run;

public sealed record RunProgress(
    int TotalRecords,
    int TestsRunning,
    int TestsCompleted,
    int Errors,
    IReadOnlyDictionary<string, int> ErrorTypes,
    int PassAtTotal,
    IReadOnlyDictionary<int, ProgressGroup> ByK,
    decimal? AverageCost,
    decimal? TotalCost);

public sealed record ProgressGroup(
    int TestsCompleted,
    int PassAtTotal,
    decimal? AverageCost,
    decimal? TotalCost);

public static class ProgressService
{
    private static readonly Regex RunPattern = new(
        @"(?:run=(?<run>\d+)/\d+.*?)?\btask=(?<task>\S+)(?:.*?\b[Kk]=(?<k>\d+))?",
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

    public static RunProgress Read(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"run log does not exist: {path}", path);

        var records = new Dictionary<string, ProgressRecord>(StringComparer.Ordinal);
        var errorTypes = new Dictionary<string, int>(StringComparer.Ordinal);
        int totalRecords = 0;

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
            Match match = RunPattern.Match(log.Message);
            if (!match.Success)
                continue;

            string key = $"{match.Groups["run"].Value}:{match.Groups["task"].Value}";
            int k = match.Groups["k"].Success
                ? int.Parse(match.Groups["k"].Value, CultureInfo.InvariantCulture)
                : 1;
            ProgressRecord record = records.TryGetValue(key, out ProgressRecord? existing)
                ? existing with { K = k }
                : new ProgressRecord(k);
            Match status = StatusPattern.Match(log.Message);
            if (status.Success)
            {
                record = record with { Status = status.Groups["status"].Value };
            }

            Match result = ResultPattern.Match(log.Message);
            if (result.Success)
            {
                record = record with
                {
                    Result = result.Groups["result"].Value,
                    Completed = record.Status == "complete" &&
                        result.Groups["result"].Value is "passed" or "completed",
                    Failed = record.Failed ||
                        result.Groups["result"].Value is not ("passed" or "completed"),
                    ErrorType = record.ErrorType ??
                        (result.Groups["result"].Value is "passed" or "completed"
                            ? null
                            : "Run")
                };
            }

            if (!string.IsNullOrWhiteSpace(log.Error) || !string.IsNullOrWhiteSpace(log.ErrorType))
            {
                record = record with
                {
                    Failed = true,
                    ErrorType = log.ErrorType ?? "Run"
                };
            }

            Match cost = CostPattern.Match(log.Message);
            if (cost.Success &&
                decimal.TryParse(cost.Groups["cost"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
            {
                record = record with { Cost = value };
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

        IEnumerable<ProgressRecord> completed = records.Values.Where(record => record.Completed && !record.Failed);
        IEnumerable<ProgressRecord> failed = records.Values.Where(record => record.Failed);
        foreach (ProgressRecord record in failed)
        {
            string type = record.ErrorType ?? "Run";
            errorTypes[type] = errorTypes.TryGetValue(type, out int count) ? count + 1 : 1;
        }
        var groups = completed
            .GroupBy(record => record.K)
            .ToDictionary(
                group => group.Key,
                group => ToGroup(group),
                EqualityComparer<int>.Default);
        List<decimal> costs = completed
            .Where(record => record.Cost.HasValue)
            .Select(record => record.Cost!.Value)
            .ToList();

        return new RunProgress(
            totalRecords,
            records.Values.Count(record =>
                !record.Completed &&
                !record.Failed &&
                record.Status is not null),
            completed.Count(),
            failed.Count(),
            errorTypes,
            completed.Count(record => string.Equals(record.Result, "passed", StringComparison.OrdinalIgnoreCase)),
            groups,
            costs.Count == 0 ? null : costs.Average(),
            costs.Count == 0 ? null : costs.Sum());
    }

    private static ProgressGroup ToGroup(IEnumerable<ProgressRecord> records)
    {
        List<ProgressRecord> completed = records.Where(record => record.Completed).ToList();
        List<decimal> costs = completed
            .Where(record => record.Cost.HasValue)
            .Select(record => record.Cost!.Value)
            .ToList();
        return new ProgressGroup(
            completed.Count,
            completed.Count(record => string.Equals(record.Result, "passed", StringComparison.OrdinalIgnoreCase)),
            costs.Count == 0 ? null : costs.Average(),
            costs.Count == 0 ? null : costs.Sum());
    }

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
        string? ErrorType = null);
    private sealed record ProgressRecord(
        int K,
        string? Status = null,
        string? Result = null,
        decimal? Cost = null,
        bool Completed = false,
        bool Failed = false,
        string? ErrorType = null);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
