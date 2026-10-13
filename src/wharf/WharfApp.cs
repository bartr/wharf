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
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += handler;
        try
        {
            var root = CommandTree.Create(configuration, cancellation.Token);
            return await root.Parse(args).InvokeAsync();
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }
}
