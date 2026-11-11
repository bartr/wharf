namespace Wharf.Configuration;

public sealed record WharfConfiguration(
    string LogFormat = "text",
    bool Quiet = false,
    int Verbosity = 0)
{
    public bool Verbose => Verbosity > 0;

    public static WharfConfiguration FromEnvironment()
    {
        string? format = Environment.GetEnvironmentVariable("WHARF_LOG_FORMAT");
        string? quiet = Environment.GetEnvironmentVariable("WHARF_QUIET");
        string? verbose = Environment.GetEnvironmentVariable("WHARF_VERBOSE");
        return new WharfConfiguration(
            string.IsNullOrWhiteSpace(format) ? "text" : format,
            IsTrue(quiet),
            ParseVerbosity(verbose));
    }

    private static bool IsTrue(string? value) =>
        string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private static int ParseVerbosity(string? value) =>
        int.TryParse(value, out int parsed)
            ? Math.Clamp(parsed, 0, 3)
            : IsTrue(value) ? 1 : 0;
}
