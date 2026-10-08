using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tomlyn;
using Tomlyn.Model;

namespace Wharf.Tasks;

public sealed class HarborTaskLoader
{
    internal const string AgentDurationEstimatesFileName = "agent-duration-estimates.json";
    private static readonly string[] ExcludedPathParts = ["solution", ".git"];
    private static readonly string[] ExcludedFileNames =
        ["test.patch", "solution.patch", "grader.py", "solve.sh"];
    private static readonly JsonSerializerOptions EstimateJsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public IReadOnlyList<HarborTask> Load(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            throw new HarborTaskException($"task root does not exist: {rootPath}");
        }

        IReadOnlyDictionary<string, AgentDurationEstimate> durationEstimates =
            LoadDurationEstimates(rootPath);
        var tasks = new List<HarborTask>();
        foreach (string directory in Directory.EnumerateDirectories(rootPath).OrderBy(path => path, StringComparer.Ordinal))
        {
            string manifestPath = Path.Combine(directory, "task.toml");
            if (File.Exists(manifestPath))
            {
                tasks.Add(LoadTask(directory, manifestPath, durationEstimates));
            }
        }

        string[] unknownEstimates = durationEstimates.Keys
            .Except(tasks.Select(task => task.TaskId), StringComparer.Ordinal)
            .OrderBy(taskId => taskId, StringComparer.Ordinal)
            .ToArray();
        if (unknownEstimates.Length > 0)
        {
            throw new HarborTaskException(
                $"agent duration estimates contain unknown tasks: {string.Join(", ", unknownEstimates)}");
        }

        return tasks;
    }

    private static HarborTask LoadTask(
        string directory,
        string manifestPath,
        IReadOnlyDictionary<string, AgentDurationEstimate> durationEstimates)
    {
        TomlTable model;
        try
        {
            model = Toml.ToModel(File.ReadAllText(manifestPath));
        }
        catch (TomlException exception)
        {
            throw new HarborTaskException($"invalid task manifest: {manifestPath}: {exception.Message}");
        }

        string schemaVersion = RequiredString(model, "schema_version", manifestPath);
        if (schemaVersion != "1.3")
        {
            throw new HarborTaskException($"unsupported task schema version: {schemaVersion}");
        }

        TomlTable task = RequiredTable(model, "task", manifestPath);
        TomlTable metadata = RequiredTable(model, "metadata", manifestPath);
        TomlTable environment = RequiredTable(model, "environment", manifestPath);
        TomlTable agent = RequiredTable(model, "agent", manifestPath);
        TomlTable verifier = RequiredTable(model, "verifier", manifestPath);
        TomlTable verifierEnvironment = RequiredTable(
            verifier,
            "environment",
            manifestPath);
        string taskId = RequiredString(metadata, "task_id", manifestPath);
        string language = RequiredString(metadata, "language", manifestPath);
        durationEstimates.TryGetValue(taskId, out AgentDurationEstimate? durationEstimate);
        if (durationEstimate is not null &&
            !string.Equals(durationEstimate.Language, language, StringComparison.Ordinal))
        {
            throw new HarborTaskException(
                $"agent duration estimate language for '{taskId}' does not match its manifest");
        }
        string instructionPath = Path.Combine(directory, "instruction.md");
        if (!File.Exists(instructionPath))
        {
            throw new HarborTaskException($"task instruction is missing: {instructionPath}");
        }

        return new HarborTask(
            taskId,
            RequiredString(task, "name", manifestPath),
            OptionalString(task, "description") ??
                RequiredString(metadata, "display_description", manifestPath),
            RequiredString(metadata, "display_title", manifestPath),
            RequiredString(metadata, "category", manifestPath),
            language,
            schemaVersion,
            directory,
            instructionPath,
            ComputeChecksum(directory),
            OptionalInt(environment, "cpus"),
            OptionalInt(environment, "memory_mb"),
            OptionalInt(environment, "storage_mb"),
            OptionalInt(environment, "gpus"),
            OptionalDouble(environment, "build_timeout_sec"),
            OptionalDouble(agent, "timeout_sec"),
            OptionalDouble(verifierEnvironment, "build_timeout_sec"),
            OptionalDouble(verifier, "timeout_sec"),
            OptionalString(environment, "docker_image"),
            durationEstimate?.MedianAgentDurationSeconds,
            durationEstimate?.SchedulingAgentDurationSeconds);
    }

    private static IReadOnlyDictionary<string, AgentDurationEstimate> LoadDurationEstimates(
        string rootPath)
    {
        string path = Path.Combine(rootPath, AgentDurationEstimatesFileName);
        if (!File.Exists(path))
        {
            return new Dictionary<string, AgentDurationEstimate>(StringComparer.Ordinal);
        }

        AgentDurationEstimateDocument document;
        try
        {
            document = JsonSerializer.Deserialize<AgentDurationEstimateDocument>(
                File.ReadAllText(path),
                EstimateJsonOptions)
                ?? throw new HarborTaskException(
                    $"agent duration estimates must contain an object: {path}");
        }
        catch (JsonException exception)
        {
            throw new HarborTaskException(
                $"invalid agent duration estimates: {path}: {exception.Message}");
        }

        if (document.SchemaVersion != "1.1")
        {
            throw new HarborTaskException(
                $"unsupported agent duration estimates schema version: {document.SchemaVersion}");
        }
        if (!string.Equals(document.Aggregation, "median", StringComparison.Ordinal) ||
            !string.Equals(document.Unit, "seconds", StringComparison.Ordinal))
        {
            throw new HarborTaskException(
                "agent duration estimates require median aggregation in seconds");
        }
        if (string.IsNullOrWhiteSpace(document.Source))
        {
            throw new HarborTaskException("agent duration estimates require a source");
        }
        if (document.Tasks is null)
        {
            throw new HarborTaskException("agent duration estimates require a tasks object");
        }
        if (document.Scheduling is null ||
            document.Scheduling.Strategy != "language-stride" ||
            string.IsNullOrWhiteSpace(document.Scheduling.Language) ||
            document.Scheduling.FirstPosition < 1 ||
            document.Scheduling.Spacing < 1)
        {
            throw new HarborTaskException("agent duration estimates require valid scheduling metadata");
        }
        foreach ((string taskId, AgentDurationEstimate estimate) in document.Tasks)
        {
            if (string.IsNullOrWhiteSpace(taskId) ||
                string.IsNullOrWhiteSpace(estimate.Language) ||
                !double.IsFinite(estimate.MedianAgentDurationSeconds) ||
                estimate.MedianAgentDurationSeconds <= 0 ||
                !double.IsFinite(estimate.SchedulingAgentDurationSeconds) ||
                estimate.SchedulingAgentDurationSeconds <= 0)
            {
                throw new HarborTaskException(
                    $"invalid agent duration estimate for task '{taskId}'");
            }
        }

        return new Dictionary<string, AgentDurationEstimate>(
            document.Tasks,
            StringComparer.Ordinal);
    }

    private static string ComputeChecksum(string directory)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                     .Where(IsChecksumInput)
                     .OrderBy(path => Path.GetRelativePath(directory, path), StringComparer.Ordinal))
        {
            string relativePath = Path.GetRelativePath(directory, path).Replace('\\', '/');
            byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath);
            hash.AppendData(BitConverter.GetBytes(pathBytes.Length));
            hash.AppendData(pathBytes);
            byte[] content = File.ReadAllBytes(path);
            hash.AppendData(BitConverter.GetBytes(content.Length));
            hash.AppendData(content);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static bool IsChecksumInput(string path)
    {
        string relative = path.Replace('\\', '/');
        string fileName = Path.GetFileName(path);
        return !ExcludedPathParts.Any(part => relative.Split('/').Contains(part, StringComparer.Ordinal)) &&
               !ExcludedFileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase);
    }

    private static string RequiredString(TomlTable table, string key, string source) =>
        table.TryGetValue(key, out object? value) && value is string text && !string.IsNullOrWhiteSpace(text)
            ? text
            : throw new HarborTaskException($"required string '{key}' is missing from {source}");

    private static string? OptionalString(TomlTable table, string key) =>
        table.TryGetValue(key, out object? value) && value is string text && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    private static TomlTable RequiredTable(TomlTable table, string key, string source) =>
        table.TryGetValue(key, out object? value) && value is TomlTable nested
            ? nested
            : throw new HarborTaskException($"required table '{key}' is missing from {source}");

    private static int? OptionalInt(TomlTable table, string key) =>
        table.TryGetValue(key, out object? value) ? Convert.ToInt32(value) : null;

    private static double? OptionalDouble(TomlTable table, string key) =>
        table.TryGetValue(key, out object? value) ? Convert.ToDouble(value) : null;

    private sealed record AgentDurationEstimateDocument(
        [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
        [property: JsonPropertyName("source")] string Source,
        [property: JsonPropertyName("aggregation")] string Aggregation,
        [property: JsonPropertyName("unit")] string Unit,
        [property: JsonPropertyName("scheduling")] AgentDurationScheduling Scheduling,
        [property: JsonPropertyName("tasks")]
        IReadOnlyDictionary<string, AgentDurationEstimate> Tasks);

    private sealed record AgentDurationScheduling(
        [property: JsonPropertyName("strategy")] string Strategy,
        [property: JsonPropertyName("language")] string Language,
        [property: JsonPropertyName("firstPosition")] int FirstPosition,
        [property: JsonPropertyName("spacing")] int Spacing);

    private sealed record AgentDurationEstimate(
        [property: JsonPropertyName("language")] string Language,
        [property: JsonPropertyName("medianAgentDurationSeconds")]
        double MedianAgentDurationSeconds,
        [property: JsonPropertyName("schedulingAgentDurationSeconds")]
        double SchedulingAgentDurationSeconds);
}
