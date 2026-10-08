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
            {
                AddError(log.Message, errorTypes);
                continue;
            }

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
            if (result.Success && record.Status == "complete")
            {
                record = record with
                {
                    Result = result.Groups["result"].Value,
                    Completed = true
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
                AddError(log.Message, errorTypes);
            }

            records[key] = record;
        }

        IEnumerable<ProgressRecord> completed = records.Values.Where(record => record.Completed);
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
            records.Values.Count(record => !record.Completed && record.Status is not null),
            completed.Count(),
            errorTypes.Values.Sum(),
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

    private static void AddError(string message, IDictionary<string, int> errorTypes)
    {
        Match match = ErrorPattern.Match(message);
        if (!match.Success)
            return;
        string error = match.Groups["error"].Value.Trim();
        int separator = error.IndexOf(':');
        string type = separator > 0 ? error[..separator] : error.Split(' ', 2)[0];
        errorTypes[type] = errorTypes.TryGetValue(type, out int count) ? count + 1 : 1;
    }

    private sealed record LogRecord(string Timestamp, string Level, string Message);
    private sealed record ProgressRecord(
        int K,
        string? Status = null,
        string? Result = null,
        decimal? Cost = null,
        bool Completed = false);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
