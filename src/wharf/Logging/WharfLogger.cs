using System.Text.Json;
using System.Globalization;
using Wharf.Configuration;

namespace Wharf.Logging;

public sealed class WharfLogger : IDisposable
{
    private readonly WharfConfiguration configuration;

    private WharfLogger(WharfConfiguration configuration) => this.configuration = configuration;

    public static WharfLogger Create(WharfConfiguration configuration) => new(configuration);

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
        if (configuration.LogFormat.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            string timestamp = utcNow.ToString("O", CultureInfo.InvariantCulture);
            writer.WriteLine(JsonSerializer.Serialize(new { timestamp, level, message }));
            return;
        }

        string humanTimestamp = DateTimeOffset.Now.ToString(
            "MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture);
        writer.WriteLine($"{humanTimestamp} {message}");
    }
}
