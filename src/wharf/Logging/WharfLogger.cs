using System.Diagnostics;
using System.Text.Json;
using System.Globalization;
using Wharf.Configuration;
using Wharf.Run;

namespace Wharf.Logging;

public sealed record StageLogContext(
    int RunNumber,
    int PlannedRuns,
    string TaskId,
    int LogicalAttempt,
    int TrialRetry,
    string Stage);

public sealed class WharfLogger : IDisposable
{
    private readonly WharfConfiguration configuration;
    private string? jsonlPath;
    private readonly bool deferJsonlPath;
    private readonly string? jsonlPointerPath;
    private readonly List<string> deferredRecords = [];
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    private readonly object sync = new();

    private WharfLogger(
        WharfConfiguration configuration,
        string? jsonlPath,
        bool deferJsonlPath = false,
        string? jsonlPointerPath = null)
    {
        this.configuration = configuration;
        this.jsonlPath = string.IsNullOrWhiteSpace(jsonlPath) ? null : Path.GetFullPath(jsonlPath);
        this.deferJsonlPath = deferJsonlPath;
        this.jsonlPointerPath = string.IsNullOrWhiteSpace(jsonlPointerPath)
            ? null
            : Path.GetFullPath(jsonlPointerPath);
        DateTimeOffset startedAt = DateTimeOffset.Now;
        Write(
            "info",
            $"started={startedAt.ToString("O", CultureInfo.InvariantCulture)}",
            configuration.Quiet ? TextWriter.Null : Console.Out,
            elapsedOverride: TimeSpan.Zero);
    }

    public static WharfLogger Create(WharfConfiguration configuration, string? jsonlPath = null) =>
        new(configuration, jsonlPath);

    public static WharfLogger CreateDeferred(
        WharfConfiguration configuration,
        string? jsonlPointerPath = null) =>
        new(
            configuration,
            null,
            deferJsonlPath: true,
            jsonlPointerPath: jsonlPointerPath);

    public string? JsonlPath
    {
        get
        {
            lock (sync)
                return jsonlPath;
        }
    }

    public void AttachJsonlPathIfUnset(string path)
    {
        string? pointerError = null;
        lock (sync)
        {
            if (jsonlPath is not null)
                return;

            jsonlPath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(jsonlPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            if (deferredRecords.Count > 0)
            {
                File.AppendAllLines(jsonlPath, deferredRecords);
                deferredRecords.Clear();
            }

            try
            {
                UpdateJsonlPointer();
            }
            catch (Exception exception) when (
                exception is IOException or
                PlatformNotSupportedException or
                UnauthorizedAccessException)
            {
                pointerError = exception.Message;
            }
        }

        if (pointerError is not null)
        {
            Info(
                $"run log pointer failed path={jsonlPointerPath} error={pointerError}",
                pointerError,
                "RunLogPointer");
        }
    }

    public void Info(string message, string? error = null, string? errorType = null)
    {
        Write(
            "info",
            message,
            configuration.Quiet ? TextWriter.Null : Console.Out,
            error,
            errorType);
    }

    public void StageStarted(StageLogContext context)
    {
        Write(
            "info",
            StageMessage(context, "started"),
            TextWriter.Null,
            fields: StageFields(context, "started"));
    }

    public void StageCompleted(
        StageLogContext context,
        TimeSpan duration,
        string result,
        string? error = null,
        string? errorType = null)
    {
        Dictionary<string, object?> fields = StageFields(context, "completed");
        fields["durationMilliseconds"] = Math.Max(0, (long)duration.TotalMilliseconds);
        fields["result"] = result;
        Write(
            "info",
            StageMessage(context, "completed"),
            TextWriter.Null,
            error,
            errorType,
            fields: fields);
    }

    internal void PreparationGateChanged(PreparationGateEvent gateEvent)
    {
        var fields = new Dictionary<string, object?>
        {
            ["eventType"] = "preparation-gate",
            ["sequence"] = gateEvent.Sequence,
            ["operation"] = gateEvent.Operation switch
            {
                PreparationOperation.Agent => "agent",
                PreparationOperation.AgentRetry => "agent-retry",
                PreparationOperation.Verifier => "verifier",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(gateEvent),
                    gateEvent.Operation,
                    "unknown preparation operation")
            },
            ["transition"] = gateEvent.Transition,
            ["capacity"] = gateEvent.Capacity,
            ["active"] = gateEvent.Active,
            ["waiting"] = gateEvent.Waiting
        };
        if (gateEvent.WaitMilliseconds is not null)
            fields["waitMilliseconds"] = gateEvent.WaitMilliseconds;
        Write(
            "info",
            $"preparation-gate operation={fields["operation"]} " +
            $"transition={gateEvent.Transition} active={gateEvent.Active} " +
            $"waiting={gateEvent.Waiting} capacity={gateEvent.Capacity}",
            TextWriter.Null,
            fields: fields);
    }

    public void Verbose(string message, int requiredLevel = 1)
    {
        if (configuration.Verbosity >= requiredLevel)
            Write("debug", message, configuration.Quiet ? TextWriter.Null : Console.Out);
    }

    public void Error(string message) => Write("error", message, Console.Error, message, "Logger");

    public void Dispose()
    {
    }

    private void UpdateJsonlPointer()
    {
        if (jsonlPointerPath is null ||
            jsonlPath is null ||
            string.Equals(jsonlPointerPath, jsonlPath, StringComparison.Ordinal))
        {
            return;
        }

        string? directory = Path.GetDirectoryName(jsonlPointerPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        if (Directory.Exists(jsonlPointerPath))
            throw new IOException($"run log pointer path is a directory: {jsonlPointerPath}");

        var pointer = new FileInfo(jsonlPointerPath);
        if (pointer.Exists && pointer.LinkTarget is null)
        {
            throw new IOException(
                $"run log pointer path is an existing regular file: {jsonlPointerPath}");
        }

        if (pointer.LinkTarget is not null)
            File.Delete(jsonlPointerPath);
        File.CreateSymbolicLink(jsonlPointerPath, jsonlPath);
    }

    private void Write(
        string level,
        string message,
        TextWriter writer,
        string? error = null,
        string? errorType = null,
        TimeSpan? elapsedOverride = null,
        IReadOnlyDictionary<string, object?>? fields = null)
    {
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        string timestamp = utcNow.ToString("O", CultureInfo.InvariantCulture);
        var record = new Dictionary<string, object?>
        {
            ["timestamp"] = timestamp,
            ["level"] = level,
            ["message"] = message
        };
        if (!string.IsNullOrWhiteSpace(error))
            record["error"] = error;
        if (!string.IsNullOrWhiteSpace(errorType))
            record["errorType"] = errorType;
        if (fields is not null)
        {
            foreach ((string key, object? value) in fields)
                record[key] = value;
        }
        string serializedRecord = JsonSerializer.Serialize(record);
        lock (sync)
        {
            if (jsonlPath is not null)
            {
                string? directory = Path.GetDirectoryName(jsonlPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
                File.AppendAllText(
                    jsonlPath,
                    serializedRecord + Environment.NewLine);
            }
            else if (deferJsonlPath)
                deferredRecords.Add(serializedRecord);
        }

        if (configuration.LogFormat.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            writer.WriteLine(JsonSerializer.Serialize(new { timestamp, level, message }));
            return;
        }

        string humanTimestamp = FormatElapsed(elapsedOverride ?? elapsed.Elapsed);
        writer.WriteLine($"{humanTimestamp} {message}");
    }

    private static string StageMessage(StageLogContext context, string transition) =>
        $"run={context.RunNumber:000}/{context.PlannedRuns:000} " +
        $"task={context.TaskId} stage={context.Stage} transition={transition} " +
        $"attempt={context.LogicalAttempt} retry={context.TrialRetry}";

    private static Dictionary<string, object?> StageFields(
        StageLogContext context,
        string transition) =>
        new()
        {
            ["eventType"] = "stage",
            ["runNumber"] = context.RunNumber,
            ["plannedRuns"] = context.PlannedRuns,
            ["taskId"] = context.TaskId,
            ["logicalAttempt"] = context.LogicalAttempt,
            ["trialRetry"] = context.TrialRetry,
            ["stage"] = context.Stage,
            ["transition"] = transition
        };

    internal static string FormatElapsed(TimeSpan value)
    {
        long hours = Math.Max(0, (long)value.TotalHours);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{hours:00}:{value.Minutes:00}:{value.Seconds:00}");
    }
}
