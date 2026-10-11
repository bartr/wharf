using System.Reflection;

namespace Wharf.Versioning;

public static class WharfVersion
{
    public static string Current =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
}
