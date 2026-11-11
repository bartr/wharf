using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wharf.State;

public static class StateSchema
{
    public const string Version = "1.0";
}

public readonly record struct RunId
{
    public RunId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new StateContractException("run ID must not be empty");
        }

        Value = value;
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct TrialId
{
    public TrialId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new StateContractException("trial ID must not be empty");
        }

        Value = value;
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct LogicalAttemptId
{
    public LogicalAttemptId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new StateContractException("logical attempt ID must not be empty");
        }

        Value = value;
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct InfrastructureRetryId
{
    public InfrastructureRetryId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new StateContractException("infrastructure retry ID must not be empty");
        }

        Value = value;
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct EventId
{
    public EventId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new StateContractException("event ID must not be empty");
        }

        Value = value;
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public sealed class StateIdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type type) =>
        type == typeof(RunId) ||
        type == typeof(TrialId) ||
        type == typeof(LogicalAttemptId) ||
        type == typeof(InfrastructureRetryId) ||
        type == typeof(EventId);

    public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(StateIdJsonConverter<>).MakeGenericType(type))!;
}

public sealed class StateIdJsonConverter<T> : JsonConverter<T>
{
    public override T Read(
        ref Utf8JsonReader reader,
        Type type,
        JsonSerializerOptions options)
    {
        string value = reader.GetString() ?? throw new JsonException("state ID must be a string");
        object result = typeof(T) switch
        {
            var matched when matched == typeof(RunId) => new RunId(value),
            var matched when matched == typeof(TrialId) => new TrialId(value),
            var matched when matched == typeof(LogicalAttemptId) => new LogicalAttemptId(value),
            var matched when matched == typeof(InfrastructureRetryId) => new InfrastructureRetryId(value),
            var matched when matched == typeof(EventId) => new EventId(value),
            _ => throw new JsonException($"unsupported state ID type: {typeof(T)}")
        };
        return (T)result;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value?.ToString());
}

public static class StateJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        options.Converters.Add(new StateIdJsonConverterFactory());
        return options;
    }
}

public static class StateEventTypes
{
    public const string RunCreated = "run.created";
    public const string TrialStarted = "trial.started";
    public const string LogicalAttemptStarted = "logical-attempt.started";
    public const string InfrastructureRetryStarted = "infrastructure-retry.started";
    public const string TrialCompleted = "trial.completed";
    public const string TrialFailed = "trial.failed";
}

public static class StateStatuses
{
    public const string Created = "created";
    public const string Starting = "starting";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

public sealed record StateEvent(
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
    [property: JsonPropertyName("eventId")] EventId EventId,
    [property: JsonPropertyName("sequence")] long Sequence,
    [property: JsonPropertyName("runId")] RunId RunId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("trialId")] TrialId? TrialId = null,
    [property: JsonPropertyName("logicalAttemptId")] LogicalAttemptId? LogicalAttemptId = null,
    [property: JsonPropertyName("infrastructureRetryId")] InfrastructureRetryId? InfrastructureRetryId = null,
    [property: JsonPropertyName("payload")] IReadOnlyDictionary<string, string>? Payload = null);

public sealed record EffectiveConfigurationEnvelope(
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
    [property: JsonPropertyName("runId")] RunId RunId,
    [property: JsonPropertyName("configuration")] JsonElement Configuration);

public sealed record TrialSummary(
    [property: JsonPropertyName("trialId")] TrialId TrialId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("logicalAttempts")] int LogicalAttempts,
    [property: JsonPropertyName("infrastructureRetries")] int InfrastructureRetries);

public sealed record RunSummary(
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
    [property: JsonPropertyName("runId")] RunId RunId,
    [property: JsonPropertyName("lastSequence")] long LastSequence,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("eventCount")] int EventCount,
    [property: JsonPropertyName("trials")] IReadOnlyDictionary<string, TrialSummary> Trials);

public sealed class StateContractException(string message) : Exception(message);
