using System.Security.Cryptography;
using System.Text;
using Tomlyn;
using Tomlyn.Model;

namespace Wharf.Tasks;

public sealed class HarborTaskLoader
{
    private static readonly string[] ExcludedPathParts = ["solution", ".git"];
    private static readonly string[] ExcludedFileNames =
        ["test.patch", "solution.patch", "grader.py", "solve.sh"];

    public IReadOnlyList<HarborTask> Load(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            throw new HarborTaskException($"task root does not exist: {rootPath}");
        }

        var tasks = new List<HarborTask>();
        foreach (string directory in Directory.EnumerateDirectories(rootPath).OrderBy(path => path, StringComparer.Ordinal))
        {
            string manifestPath = Path.Combine(directory, "task.toml");
            if (File.Exists(manifestPath))
            {
                tasks.Add(LoadTask(directory, manifestPath));
            }
        }

        return tasks;
    }

    private static HarborTask LoadTask(string directory, string manifestPath)
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
        TomlTable verifier = RequiredTable(model, "verifier", manifestPath);
        string taskId = RequiredString(metadata, "task_id", manifestPath);
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
            RequiredString(metadata, "language", manifestPath),
            schemaVersion,
            directory,
            instructionPath,
            ComputeChecksum(directory),
            OptionalInt(environment, "cpus"),
            OptionalInt(environment, "memory_mb"),
            OptionalInt(environment, "storage_mb"),
            OptionalInt(environment, "gpus"),
            OptionalDouble(environment, "build_timeout_sec"),
            OptionalDouble(verifier, "agent_timeout_sec"),
            OptionalDouble(verifier, "timeout_sec"),
            OptionalString(environment, "docker_image"));
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
}
