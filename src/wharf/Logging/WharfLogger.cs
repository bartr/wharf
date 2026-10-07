using System.Text.Json;
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
        if (configuration.LogFormat.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            writer.WriteLine(JsonSerializer.Serialize(new { level, message }));
            return;
        }

        writer.WriteLine(message);
    }
}
