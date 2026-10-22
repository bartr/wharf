using System.Text.Json;
using Wharf.State;

namespace Wharf.Reporting;

public sealed class DataManifestBuilder
{
    public IReadOnlyList<DataManifestEntry> Build(
        string dataDirectory,
        string? manifestPath = null)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            throw new AnalyticsEvidenceException("analytics data directory is required");
        }

        string fullDataDirectory = Path.GetFullPath(dataDirectory);
        Directory.CreateDirectory(fullDataDirectory);
        var entries = new List<DataManifestEntry>();
        foreach (string path in Directory.EnumerateFiles(
                     fullDataDirectory, "*.jsonl", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            IReadOnlyList<AnalyticsTrial> rows = ReadRows(path);
            string arm = rows.Select(row => row.Arm)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                ?? Path.GetFileNameWithoutExtension(path);
            if (rows.Any(row => !string.Equals(row.Arm, arm, StringComparison.Ordinal)))
            {
                throw new AnalyticsEvidenceException(
                    $"analytics file '{path}' contains multiple arms");
            }
            if (rows.Select(row => row.Trial).Distinct(StringComparer.Ordinal).Count() != rows.Count)
            {
                throw new AnalyticsEvidenceException(
                    $"analytics file '{path}' contains duplicate logical trials");
            }

            decimal[] costs = rows
                .Where(row => row.CostUsd.HasValue)
                .Select(row => row.CostUsd!.Value)
                .ToArray();
            double[] agentDurations = rows
                .Where(row => row.AgentSeconds.HasValue)
                .Select(row => row.AgentSeconds!.Value)
                .ToArray();
            int passes = rows.Count(row => row.Reward is > 0);
            int p2pErrors = rows.Count(row =>
                row.RegressionTestsPassed.HasValue &&
                row.RegressionTestsTotal.HasValue &&
                row.RegressionTestsPassed < row.RegressionTestsTotal);
            entries.Add(new DataManifestEntry(
                arm,
                new FileInfo(path).Length,
                SingleValue(rows.Select(row => row.Agent), path, "CLI"),
                SingleValue(rows.Select(row => row.AgentVersion), path, "CLI version"),
                SingleValue(rows.Select(row => row.Effort), path, "effort"),
                SingleValue(rows.Select(row => row.Model), path, "model"),
                passes,
                p2pErrors,
                rows.Count == 0 ? null : (decimal)passes / rows.Count - p2pErrors * 0.0025m,
                rows.Count(row => row.Reward.HasValue),
                rows.Select(row => row.TaskId).Distinct(StringComparer.Ordinal).Count(),
                costs.Length == 0
                    ? null
                    : decimal.Round(costs.Sum() / costs.Length, 4),
                agentDurations.Length == 0
                    ? null
                    : Math.Round(agentDurations.Average(), 1),
                costs.Length == 0 ? null : decimal.Round(costs.Sum(), 2),
                rows.Count,
                costs.Length));
        }

        DataManifestEntry[] ordered = entries
            .OrderBy(entry => entry.Arm, StringComparer.Ordinal)
            .ToArray();
        string outputPath = Path.GetFullPath(
            manifestPath ?? Path.Combine(fullDataDirectory, "manifest.json"));
        var options = new JsonSerializerOptions(StateJson.Options) { WriteIndented = true };
        AtomicFile.Replace(
            outputPath,
            JsonSerializer.Serialize(ordered, options) + Environment.NewLine);
        return ordered;
    }

    private static string? SingleValue(
        IEnumerable<string?> values,
        string path,
        string description)
    {
        string[] distinctValues = values
            .OfType<string>()
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (distinctValues.Length > 1)
        {
            throw new AnalyticsEvidenceException(
                $"analytics file '{path}' contains multiple {description} values");
        }
        return distinctValues.FirstOrDefault();
    }

    private static IReadOnlyList<AnalyticsTrial> ReadRows(string path)
    {
        var rows = new List<AnalyticsTrial>();
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
                AnalyticsTrial? row = JsonSerializer.Deserialize<AnalyticsTrial>(
                    line, StateJson.Options);
                rows.Add(row ?? throw new JsonException("row is null"));
            }
            catch (JsonException exception)
            {
                throw new AnalyticsEvidenceException(
                    $"invalid analytics row '{path}' line {lineNumber}: {exception.Message}");
            }
        }
        return rows;
    }
}
