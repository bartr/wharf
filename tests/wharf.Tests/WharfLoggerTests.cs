using Wharf.Logging;
using Wharf.Configuration;
using System.Text.Json;

namespace Wharf.Tests;

public sealed class WharfLoggerTests
{
    [Theory]
    [InlineData(0, "00:00:00")]
    [InlineData(3_723, "01:02:03")]
    [InlineData(360_061, "100:01:01")]
    public void FormatsElapsedTimeWithoutDayWrapping(int seconds, string expected)
    {
        Assert.Equal(expected, WharfLogger.FormatElapsed(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void DeferredLoggerFlushesInitialRecordWhenRunPathIsAttached()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"wharf-logger-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "run.jsonl");
        string pointerPath = Path.Combine(directory, "progress.jsonl");
        try
        {
            using WharfLogger logger = WharfLogger.CreateDeferred(
                Configuration(),
                pointerPath);
            logger.Info("before-attach");

            logger.AttachJsonlPathIfUnset(path);
            logger.Info("after-attach");

            Assert.Equal(Path.GetFullPath(path), logger.JsonlPath);
            string[] lines = File.ReadAllLines(path);
            Assert.Equal(3, lines.Length);
            Assert.Contains("started=", Message(lines[0]));
            Assert.Equal("before-attach", Message(lines[1]));
            Assert.Equal("after-attach", Message(lines[2]));
            Assert.Equal(path, new FileInfo(pointerPath).LinkTarget);
            Assert.Equal(lines, File.ReadAllLines(pointerPath));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DeferredLoggerDoesNotOverwriteRegularPointerFile()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"wharf-logger-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "run.jsonl");
        string pointerPath = Path.Combine(directory, "progress.jsonl");
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(pointerPath, "keep-me");
            using WharfLogger logger = WharfLogger.CreateDeferred(
                Configuration(),
                pointerPath);

            logger.AttachJsonlPathIfUnset(path);

            Assert.Equal("keep-me", File.ReadAllText(pointerPath));
            Assert.Contains(
                File.ReadLines(path),
                line => Message(line).Contains("run log pointer failed", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ExplicitLogPathIsNotReplacedByRunDefault()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"wharf-logger-{Guid.NewGuid():N}");
        string explicitPath = Path.Combine(directory, "explicit.jsonl");
        string defaultPath = Path.Combine(directory, "run.jsonl");
        try
        {
            using WharfLogger logger = WharfLogger.Create(Configuration(), explicitPath);

            logger.AttachJsonlPathIfUnset(defaultPath);
            logger.Info("kept-explicit");

            Assert.True(File.Exists(explicitPath));
            Assert.False(File.Exists(defaultPath));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static WharfConfiguration Configuration() =>
        new(LogFormat: "text", Quiet: true, Verbosity: 0);

    private static string Message(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("message").GetString()!;
    }
}
