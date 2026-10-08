using System.Diagnostics;
using System.Text.Json;
using System.Globalization;
using Wharf.Configuration;

namespace Wharf.Logging;

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
        TimeSpan? elapsedOverride = null)
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

    internal static string FormatElapsed(TimeSpan value)
    {
        long hours = Math.Max(0, (long)value.TotalHours);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{hours:00}:{value.Minutes:00}:{value.Seconds:00}");
    }
}
