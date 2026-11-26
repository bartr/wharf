using System.Text;
using System.Text.Json;
using Wharf.State;

namespace Wharf.Reporting;

public sealed class AnalyticsExporter(RunEvidenceReader? reader = null)
{
    private readonly RunEvidenceReader reader = reader ?? new RunEvidenceReader();

    public IReadOnlyList<AnalyticsTrial> Export(
        string runDirectory,
        string outputPath,
        AnalyticsExportOptions options)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new AnalyticsEvidenceException("analytics output path is required");
        }

        IReadOnlyList<AnalyticsTrial> rows = reader.Read(runDirectory, options)
            .OrderBy(row => row.Trial, StringComparer.Ordinal)
            .ToArray();
        Write(outputPath, rows, overwrite: true);
        return rows;
    }

    internal static bool Write(
        string outputPath,
        IReadOnlyList<AnalyticsTrial> rows,
        bool overwrite)
    {
        if (rows.Select(row => (row.Job, row.Trial)).Distinct().Count() != rows.Count)
        {
            throw new AnalyticsEvidenceException(
                "analytics rows must have unique logical-trial identities");
        }

        var jsonl = new StringBuilder();
        foreach (AnalyticsTrial row in rows)
        {
            jsonl.AppendLine(JsonSerializer.Serialize(row, StateJson.Options));
        }
        string fullOutputPath = Path.GetFullPath(outputPath);
        string? outputDirectory = Path.GetDirectoryName(fullOutputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }
        if (overwrite)
        {
            AtomicFile.Replace(fullOutputPath, jsonl.ToString());
            return true;
        }
        return AtomicFile.TryCreate(fullOutputPath, jsonl.ToString());
    }
}
