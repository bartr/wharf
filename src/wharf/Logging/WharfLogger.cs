using System.Text.Json;
using System.Globalization;
using Wharf.Configuration;

namespace Wharf.Logging;

public sealed class WharfLogger : IDisposable
{
    private readonly WharfConfiguration configuration;
    private readonly string? jsonlPath;
    private readonly object sync = new();

    private WharfLogger(WharfConfiguration configuration, string? jsonlPath)
    {
        this.configuration = configuration;
        this.jsonlPath = string.IsNullOrWhiteSpace(jsonlPath) ? null : Path.GetFullPath(jsonlPath);
    }

    public static WharfLogger Create(WharfConfiguration configuration, string? jsonlPath = null) =>
        new(configuration, jsonlPath);

    public void Info(string message)
    {
        if (configuration.Quiet)
        {
            return;
        }

        Write("info", message, Console.Out);
    }

    public void Verbose(string message, int requiredLevel = 1)
    {
        if (!configuration.Quiet && configuration.Verbosity >= requiredLevel)
        {
            Write("debug", message, Console.Out);
        }
    }

    public void Error(string message) => Write("error", message, Console.Error);

    public void Dispose()
    {
    }

    private void Write(string level, string message, TextWriter writer)
    {
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        string timestamp = utcNow.ToString("O", CultureInfo.InvariantCulture);
        if (jsonlPath is not null)
        {
            lock (sync)
            {
                string? directory = Path.GetDirectoryName(jsonlPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(
                    jsonlPath,
                    JsonSerializer.Serialize(new { timestamp, level, message }) +
                    Environment.NewLine);
            }
        }

        if (configuration.LogFormat.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            writer.WriteLine(JsonSerializer.Serialize(new { timestamp, level, message }));
            return;
        }

        string humanTimestamp = DateTimeOffset.Now.ToString(
            "MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture);
        writer.WriteLine($"{humanTimestamp} {message}");
    }
}
