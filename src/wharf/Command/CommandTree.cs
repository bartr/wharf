using System.CommandLine;
using Wharf.Configuration;
using Wharf.ExitCodes;
using Wharf.Logging;
using Wharf.Versioning;
using CliCommand = System.CommandLine.Command;

namespace Wharf.Command;

public static class CommandTree
{
    public static RootCommand Create(WharfConfiguration configuration)
    {
        var root = new RootCommand("Run trustworthy software evaluations on customer-owned infrastructure.");
        var quiet = new Option<bool>("--quiet") { Description = "Suppress non-error output." };
        var verbose = new Option<bool>("--verbose") { Description = "Enable verbose output." };
        var logFormat = new Option<string>("--log-format")
        {
            Description = "Log format: text or json.",
            DefaultValueFactory = _ => configuration.LogFormat
        };
        root.Options.Add(quiet);
        root.Options.Add(verbose);
        root.Options.Add(logFormat);

        root.SetAction(parseResult =>
        {
            if (parseResult.GetValue(quiet))
            {
                configuration = configuration with { Quiet = true };
            }

            if (parseResult.GetValue(verbose))
            {
                configuration = configuration with { Verbose = true };
            }

            string? selectedFormat = parseResult.GetValue(logFormat);
            if (!string.IsNullOrWhiteSpace(selectedFormat))
            {
                configuration = configuration with { LogFormat = selectedFormat };
            }

            using var logger = WharfLogger.Create(configuration);
            logger.Info($"wharf {WharfVersion.Current}");
            return ExitCode.Success.Value;
        });

        root.Subcommands.Add(CreateVersionCommand(configuration));
        root.Subcommands.Add(CreateCompletionCommand());
        foreach (string? name in new[] { "check", "smoke", "run", "resume", "status", "stop", "report" })
        {
            root.Subcommands.Add(CreatePlaceholderCommand(name));
        }

        return root;
    }

    private static CliCommand CreateVersionCommand(WharfConfiguration configuration)
    {
        var command = new CliCommand("version", "Print the Wharf version.");
        command.SetAction(parseResult =>
        {
            using var logger = WharfLogger.Create(configuration);
            logger.Info(WharfVersion.Current);
            return ExitCode.Success.Value;
        });
        return command;
    }

    private static CliCommand CreateCompletionCommand()
    {
        var shell = new Argument<string>("shell");
        var command = new CliCommand("completion", "Print shell completion instructions.");
        command.Arguments.Add(shell);
        command.SetAction(parseResult =>
        {
            string? value = parseResult.GetValue(shell);
            if (value is not ("bash" or "zsh"))
            {
                Console.Error.WriteLine("completion shell must be bash or zsh");
                return ExitCode.InvalidInput.Value;
            }

            Console.WriteLine($"# Wharf {value} completion will be available in a later session.");
            return ExitCode.Success.Value;
        });
        return command;
    }

    private static CliCommand CreatePlaceholderCommand(string name)
    {
        var command = new CliCommand(name, $"Execute the {name} workflow.");
        command.SetAction(_ =>
        {
            Console.Error.WriteLine($"wharf {name} is not implemented in {WharfVersion.Current}");
            return ExitCode.InternalFailure.Value;
        });
        return command;
    }
}
