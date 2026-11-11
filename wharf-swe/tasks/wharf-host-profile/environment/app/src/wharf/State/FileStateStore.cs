using System.Text.Json;
using Wharf.Configuration;

namespace Wharf.State;

public sealed class FileStateStore
{
    private readonly string rootDirectory;

    public FileStateStore(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new StateContractException("state root directory must not be empty");
        }

        this.rootDirectory = rootDirectory;
        Directory.CreateDirectory(rootDirectory);
    }

    public RunId CreateRun(RunConfiguration configuration, RunId? runId = null)
    {
        RunId id = runId ?? new RunId(Guid.NewGuid().ToString("N"));
        string directory = GetRunDirectory(id);
        if (Directory.Exists(directory))
        {
            throw new StateContractException($"run already exists: {id}");
        }

        Directory.CreateDirectory(directory);
        string configurationJson = RunConfigurationRenderer.Render(configuration, "json");
        using JsonDocument document = JsonDocument.Parse(configurationJson);
        var envelope = new EffectiveConfigurationEnvelope(
            StateSchema.Version,
            id,
            document.RootElement.Clone());
        AtomicFile.Replace(
            GetConfigurationPath(id),
            JsonSerializer.Serialize(envelope, StateJson.Options));
        AtomicFile.Replace(
            GetSummaryPath(id),
            JsonSerializer.Serialize(
                StateReplay.Materialize(id, []),
            StateJson.Options));
        return id;
    }

    public void Append(StateEvent stateEvent)
    {
        string directory = GetRunDirectory(stateEvent.RunId);
        if (!Directory.Exists(directory))
        {
            throw new StateContractException($"run does not exist: {stateEvent.RunId}");
        }

        string eventsPath = GetEventsPath(stateEvent.RunId);
        using FileStream lockStream = new(
            $"{eventsPath}.lock",
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);
        IReadOnlyList<StateEvent> existing = StateReplay.ReadEvents(eventsPath);
        StateEvent? duplicate = existing.FirstOrDefault(item => item.EventId == stateEvent.EventId);
        if (duplicate is not null)
        {
            if (!JsonSerializer.Serialize(duplicate, StateJson.Options)
                .Equals(JsonSerializer.Serialize(stateEvent, StateJson.Options), StringComparison.Ordinal))
            {
                throw new StateContractException($"event ID already exists with different content: {stateEvent.EventId}");
            }

            return;
        }

        long expectedSequence = existing.Count + 1L;
        if (stateEvent.Sequence != expectedSequence)
        {
            throw new StateContractException(
                $"event sequence must be {expectedSequence}, got {stateEvent.Sequence}");
        }

        string line = JsonSerializer.Serialize(stateEvent, StateJson.Options) + Environment.NewLine;
        using (FileStream stream = new(eventsPath, FileMode.Append, FileAccess.Write, FileShare.Read))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(line);
            writer.Flush();
            stream.Flush(true);
        }

        WriteSummary(stateEvent.RunId);
    }

    public RunSummary ReadSummary(RunId runId)
    {
        string path = GetSummaryPath(runId);
        if (!File.Exists(path))
        {
            throw new StateContractException($"run summary does not exist: {runId}");
        }

        try
        {
            return JsonSerializer.Deserialize<RunSummary>(
                File.ReadAllText(path),
                StateJson.Options) ?? throw new StateContractException("run summary is null");
        }
        catch (JsonException exception)
        {
            throw new StateContractException($"invalid run summary: {exception.Message}");
        }
    }

    public RunSummary Replay(RunId runId)
    {
        RunSummary summary = StateReplay.Materialize(runId, StateReplay.ReadEvents(GetEventsPath(runId)));
        WriteSummary(runId, summary);
        return summary;
    }

    private void WriteSummary(RunId runId) =>
        WriteSummary(runId, StateReplay.Materialize(runId, StateReplay.ReadEvents(GetEventsPath(runId))));

    private void WriteSummary(RunId runId, RunSummary summary) =>
        AtomicFile.Replace(GetSummaryPath(runId), JsonSerializer.Serialize(summary, StateJson.Options));

    private string GetRunDirectory(RunId runId) => Path.Combine(rootDirectory, runId.Value);
    private string GetEventsPath(RunId runId) => Path.Combine(GetRunDirectory(runId), "events.jsonl");
    private string GetConfigurationPath(RunId runId) => Path.Combine(GetRunDirectory(runId), "configuration.json");
    private string GetSummaryPath(RunId runId) => Path.Combine(GetRunDirectory(runId), "summary.json");
}
