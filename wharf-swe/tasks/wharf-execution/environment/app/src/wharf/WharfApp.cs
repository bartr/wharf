using System.CommandLine;
using Wharf.Command;
using Wharf.Configuration;
using Wharf.ExitCodes;
using Wharf.Logging;
using Wharf.Versioning;

namespace Wharf;

public static class WharfApp
{
    public static async Task<int> RunAsync(string[] args)
    {
        var configuration = WharfConfiguration.FromEnvironment();
        var root = CommandTree.Create(configuration);
        return await root.Parse(args).InvokeAsync();
    }
}
