using Wharf.Logging;

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
}
