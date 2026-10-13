using System.Text.Json;
using Wharf.Configuration;
using Wharf.State;
using Wharf.Tasks;

namespace Wharf.Recovery;

internal sealed record RetainedTaskIdentity(
    string TaskId,
    string SourceChecksum);

internal sealed record RetainedRunMetadata(
    string SchemaVersion,
    string TaskRoot,
    RunConfiguration Configuration,
    IReadOnlyList<RetainedTaskIdentity> Tasks);

internal static class RetainedRunMetadataStore
{
    public const string FileName = "run-plan.json";
    private const string SchemaVersion = "1.0";

    public static void Write(
        string runDirectory,
        string taskRoot,
        RunConfiguration configuration,
        IReadOnlyList<HarborTask> tasks)
    {
        var metadata = new RetainedRunMetadata(
            SchemaVersion,
            Path.GetFullPath(taskRoot),
            configuration with { Credentials = new SecretSettings() },
            tasks.Select(task => new RetainedTaskIdentity(
                    task.TaskId,
                    task.SourceChecksum))
                .ToArray());
        AtomicFile.Replace(
            Path.Combine(runDirectory, FileName),
            JsonSerializer.Serialize(metadata, StateJson.Options));
    }

    public static RetainedRunMetadata? Read(string runDirectory)
    {
        string path = Path.Combine(runDirectory, FileName);
        if (!File.Exists(path))
            return null;

        try
        {
            RetainedRunMetadata metadata = JsonSerializer.Deserialize<RetainedRunMetadata>(
                File.ReadAllText(path),
                StateJson.Options) ?? throw new RecoveryException("resume run plan is null");
            if (!string.Equals(metadata.SchemaVersion, SchemaVersion, StringComparison.Ordinal))
            {
                throw new RecoveryException(
                    $"unsupported resume run plan schema: {metadata.SchemaVersion}");
            }

            if (string.IsNullOrWhiteSpace(metadata.TaskRoot) || metadata.Tasks.Count == 0)
                throw new RecoveryException("resume run plan is incomplete");
            if (metadata.Tasks.Select(task => task.TaskId).Distinct(StringComparer.Ordinal).Count() !=
                metadata.Tasks.Count)
            {
                throw new RecoveryException("resume run plan contains duplicate task IDs");
            }

            return metadata;
        }
        catch (JsonException exception)
        {
            throw new RecoveryException(
                $"resume run plan contains malformed JSON: {path}: {exception.Message}");
        }
    }
}
