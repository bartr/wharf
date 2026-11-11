using System.CommandLine;
using System.Text.Json;
using Wharf.Configuration;
using Wharf.ExitCodes;
using Wharf.Host;
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
        root.Subcommands.Add(CreateRunCommand(configuration, logFormat));
        root.Subcommands.Add(CreateCheckCommand());
        foreach (string? name in new[] { "smoke", "resume", "status", "stop", "report" })
        {
            root.Subcommands.Add(CreatePlaceholderCommand(name));
        }

        return root;
    }

    private static CliCommand CreateCheckCommand()
    {
        var command = new CliCommand("check", "Validate the host and Docker prerequisites.");
        var allowUnsupported = new Option<bool>("--allow-unsupported-host")
        {
            Description = "Continue past platform classification failures with a warning."
        };
        var json = new Option<bool>("--json") { Description = "Render the report as JSON." };
        command.Options.Add(allowUnsupported);
        command.Options.Add(json);
        command.SetAction(parseResult =>
        {
            HostCheckReport report = new HostCheckService(
                [new PlatformProbe(), new DockerProbe(), new CapacityProbe()])
                .Check(new HostCheckOptions(parseResult.GetValue(allowUnsupported)));
            if (parseResult.GetValue(json))
            {
                Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
            }
            else
            {
                foreach (HostCheck check in report.Checks)
                {
                    Console.WriteLine($"{(check.Passed ? "PASS" : "FAIL")} {check.Name}: {check.Detail}");
                }
            }

            return report.ExitCode;
        });
        return command;
    }

    private static CliCommand CreateRunCommand(
        WharfConfiguration configuration,
        Option<string> logFormat)
    {
        var command = new CliCommand("run", "Execute or preview a configured evaluation run.");
        var manifest = new Option<string?>("--manifest") { Description = "Path to a JSON run manifest." };
        var dryRun = new Option<bool>("--dry-run") { Description = "Validate and print a side-effect-free plan." };
        dryRun.Aliases.Add("-d");
        var k = new Option<int?>("--k") { Description = "Attempts per task." };
        var n = new Option<int?>("--n") { Description = "Task concurrency." };
        var b = new Option<int?>("--b") { Description = "Preparation/build concurrency." };
        var outputFormat = new Option<string?>("--output-format") { Description = "Output format: text or json." };
        var outputDirectory = new Option<string?>("--output-directory") { Description = "Relative output directory." };
        var model = new Option<string?>("--model") { Description = "Model name." };
        var effort = new Option<string?>("--effort") { Description = "Model effort: low, medium, or high." };
        command.Options.Add(manifest);
        command.Options.Add(dryRun);
        command.Options.Add(k);
        command.Options.Add(n);
        command.Options.Add(b);
        command.Options.Add(outputFormat);
        command.Options.Add(outputDirectory);
        command.Options.Add(model);
        command.Options.Add(effort);
        command.SetAction(parseResult =>
        {
            if (!parseResult.GetValue(dryRun))
            {
                Console.Error.WriteLine($"wharf run is not implemented in {WharfVersion.Current}");
                return ExitCode.InternalFailure.Value;
            }

            try
            {
                string? rendered = DryRunService.Execute(
                    parseResult.GetValue(manifest),
                    new RunConfigurationOverrides(
                        parseResult.GetValue(k),
                        parseResult.GetValue(n),
                        parseResult.GetValue(b),
                        parseResult.GetValue(outputFormat),
                        parseResult.GetValue(outputDirectory),
                        parseResult.GetValue(model),
                        parseResult.GetValue(effort)),
                    parseResult.GetValue(outputFormat));
                Console.WriteLine(rendered);
                return ExitCode.Success.Value;
            }
            catch (RunConfigurationException exception)
            {
                Console.Error.WriteLine(exception.Message);
                return ExitCode.InvalidInput.Value;
            }
        });
        return command;
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
