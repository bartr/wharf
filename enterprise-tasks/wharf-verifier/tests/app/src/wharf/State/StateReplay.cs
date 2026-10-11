using System.Text.Json;

namespace Wharf.State;

public static class StateReplay
{
    public static IReadOnlyList<StateEvent> ReadEvents(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var events = new List<StateEvent>();
        long expectedSequence = 1;
        foreach (string line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                throw new StateContractException("event stream contains an empty record");
            }

            StateEvent? stateEvent;
            try
            {
                stateEvent = JsonSerializer.Deserialize<StateEvent>(line, StateJson.Options);
            }
            catch (JsonException exception)
            {
                throw new StateContractException($"invalid event record: {exception.Message}");
            }

            if (stateEvent is null)
            {
                throw new StateContractException("event record must not be null");
            }

            ValidateEvent(stateEvent, expectedSequence);
            events.Add(stateEvent);
            expectedSequence++;
        }

        return events;
    }

    public static RunSummary Materialize(RunId runId, IReadOnlyList<StateEvent> events)
    {
        var trials = new Dictionary<string, TrialSummary>(StringComparer.Ordinal);
        var appliedEventIds = new HashSet<EventId>();
        string status = StateStatuses.Created;

        foreach (StateEvent stateEvent in events.OrderBy(item => item.Sequence))
        {
            if (!appliedEventIds.Add(stateEvent.EventId))
            {
                continue;
            }

            if (stateEvent.RunId != runId)
            {
                throw new StateContractException("event belongs to a different run");
            }

            status = stateEvent.Status;
            if (stateEvent.TrialId is not TrialId trialId)
            {
                continue;
            }

            string key = trialId.Value;
            trials.TryGetValue(key, out TrialSummary? current);
            int attempts = current?.LogicalAttempts ?? 0;
            int retries = current?.InfrastructureRetries ?? 0;
            if (stateEvent.EventType == StateEventTypes.LogicalAttemptStarted)
            {
                attempts++;
            }

            if (stateEvent.EventType == StateEventTypes.InfrastructureRetryStarted)
            {
                retries++;
            }

            trials[key] = new TrialSummary(
                trialId,
                stateEvent.Status,
                attempts,
                retries);
        }

        return new RunSummary(
            StateSchema.Version,
            runId,
            events.Count == 0 ? 0 : events.Max(item => item.Sequence),
            status,
            appliedEventIds.Count,
            trials);
    }

    private static void ValidateEvent(StateEvent stateEvent, long expectedSequence)
    {
        if (stateEvent.SchemaVersion != StateSchema.Version)
        {
            throw new StateContractException(
                $"unsupported event schema version: {stateEvent.SchemaVersion}");
        }

        if (stateEvent.Sequence != expectedSequence)
        {
            throw new StateContractException(
                $"event sequence must be {expectedSequence}, got {stateEvent.Sequence}");
        }

        if (string.IsNullOrWhiteSpace(stateEvent.EventType) ||
            string.IsNullOrWhiteSpace(stateEvent.Status))
        {
            throw new StateContractException("event type and status must not be empty");
        }
    }
}
