using Wharf.Configuration;
using Wharf.State;

namespace Wharf.Tests;

public sealed class StateStoreTests
{
    [Fact]
    public void AppendsReplaysAndDeduplicatesEvents()
    {
        using TemporaryDirectory temporary = new();
        var store = new FileStateStore(temporary.Path);
        RunId runId = store.CreateRun(CreateConfiguration(), new RunId("run-1"));
        var trialId = new TrialId("trial-1");
        var logicalAttemptId = new LogicalAttemptId("attempt-1");
        var retryId = new InfrastructureRetryId("retry-1");
        StateEvent created = Event(runId, "event-1", 1, StateEventTypes.RunCreated, StateStatuses.Created);
        StateEvent attempt = Event(
            runId,
            "event-2",
            2,
            StateEventTypes.LogicalAttemptStarted,
            StateStatuses.Running,
            trialId,
            logicalAttemptId);
        StateEvent retry = Event(
            runId,
            "event-3",
            3,
            StateEventTypes.InfrastructureRetryStarted,
            StateStatuses.Running,
            trialId,
            infrastructureRetryId: retryId);

        store.Append(created);
        store.Append(attempt);
        store.Append(retry);
        store.Append(retry);

        RunSummary summary = store.Replay(runId);

        Assert.Equal(3, summary.EventCount);
        Assert.Equal(3, summary.LastSequence);
        Assert.Equal(1, summary.Trials["trial-1"].LogicalAttempts);
        Assert.Equal(1, summary.Trials["trial-1"].InfrastructureRetries);
        RunSummary reloaded = store.ReadSummary(runId);
        Assert.Equal(summary.LastSequence, reloaded.LastSequence);
        Assert.Equal(summary.EventCount, reloaded.EventCount);
        Assert.Equal(summary.Trials["trial-1"], reloaded.Trials["trial-1"]);
        byte[] beforeReplay = File.ReadAllBytes(Path.Combine(temporary.Path, "run-1", "summary.json"));
        store.Replay(runId);
        Assert.Equal(beforeReplay, File.ReadAllBytes(Path.Combine(temporary.Path, "run-1", "summary.json")));
    }

    [Fact]
    public void ConflictingDuplicateEventIsRejected()
    {
        using TemporaryDirectory temporary = new();
        var store = new FileStateStore(temporary.Path);
        RunId runId = store.CreateRun(CreateConfiguration(), new RunId("run-2"));
        store.Append(Event(runId, "event-1", 1, StateEventTypes.RunCreated, StateStatuses.Created));

        StateContractException exception = Assert.Throws<StateContractException>(
            () => store.Append(Event(runId, "event-1", 1, StateEventTypes.RunCreated, StateStatuses.Failed)));

        Assert.Contains("different content", exception.Message);
    }

    [Fact]
    public void CorruptEventStreamFailsExplicitly()
    {
        using TemporaryDirectory temporary = new();
        var store = new FileStateStore(temporary.Path);
        RunId runId = store.CreateRun(CreateConfiguration(), new RunId("run-3"));
        store.Append(Event(runId, "event-1", 1, StateEventTypes.RunCreated, StateStatuses.Created));
        File.AppendAllText(Path.Combine(temporary.Path, "run-3", "events.jsonl"), "{truncated");

        StateContractException exception = Assert.Throws<StateContractException>(
            () => store.Replay(runId));

        Assert.Contains("invalid event record", exception.Message);
    }

    [Fact]
    public void SchemaAndSequenceMismatchesFailExplicitly()
    {
        using TemporaryDirectory temporary = new();
        var store = new FileStateStore(temporary.Path);
        RunId runId = store.CreateRun(CreateConfiguration(), new RunId("run-5"));
        string eventsPath = Path.Combine(temporary.Path, "run-5", "events.jsonl");
        StateEvent invalid = Event(runId, "event-1", 2, StateEventTypes.RunCreated, StateStatuses.Created)
            with
        { SchemaVersion = "2.0" };
        File.WriteAllText(eventsPath, System.Text.Json.JsonSerializer.Serialize(invalid, StateJson.Options));

        StateContractException exception = Assert.Throws<StateContractException>(
            () => store.Replay(runId));

        Assert.Contains("unsupported event schema version", exception.Message);
    }

    [Fact]
    public async Task ReadersCanReadWhileEventsAreAppended()
    {
        using TemporaryDirectory temporary = new();
        var store = new FileStateStore(temporary.Path);
        RunId runId = store.CreateRun(CreateConfiguration(), new RunId("run-4"));
        Task writer = Task.Run(() =>
        {
            for (int index = 1; index <= 20; index++)
            {
                store.Append(Event(
                    runId,
                    $"event-{index}",
                    index,
                    index == 1 ? StateEventTypes.RunCreated : StateEventTypes.TrialStarted,
                    index == 1 ? StateStatuses.Created : StateStatuses.Running,
                    index == 1 ? null : new TrialId($"trial-{index}")));
            }
        });

        while (!writer.IsCompleted)
        {
            RunSummary summary = store.ReadSummary(runId);
            Assert.True(summary.EventCount >= 0);
        }

        await writer;
        Assert.Equal(20, store.ReadSummary(runId).EventCount);
    }

    private static StateEvent Event(
        RunId runId,
        string eventId,
        long sequence,
        string type,
        string status,
        TrialId? trialId = null,
        LogicalAttemptId? logicalAttemptId = null,
        InfrastructureRetryId? infrastructureRetryId = null) =>
        new(
            StateSchema.Version,
            new EventId(eventId),
            sequence,
            runId,
            type,
            status,
            trialId,
            logicalAttemptId,
            infrastructureRetryId);

    private static RunConfiguration CreateConfiguration() =>
        RunConfigurationResolver.Resolve(
            new RunManifest(),
            new RunConfigurationOverrides(),
            new Dictionary<string, string?>());

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"wharf-state-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
