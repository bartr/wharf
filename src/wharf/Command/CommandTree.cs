using System.CommandLine;
using System.Text.Json;
using Wharf.Configuration;
using Wharf.ExitCodes;
using Wharf.Host;
using Wharf.Logging;
using Wharf.Reporting;
using Wharf.Run;
using Wharf.Smoke;
using Wharf.Qualification;
using Wharf.Tasks;
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
        verbose.Aliases.Add("-v");
        var veryVerbose = new Option<bool>("-vv") { Description = "Show each registry rate-limit retry." };
        var diagnostic = new Option<bool>("-vvv") { Description = "Show redacted subprocess diagnostics." };
        var logFormat = new Option<string>("--log-format")
        {
            Description = "Log format: text or json.",
            DefaultValueFactory = _ => configuration.LogFormat
        };
        root.Options.Add(quiet);
        root.Options.Add(verbose);
        root.Options.Add(veryVerbose);
        root.Options.Add(diagnostic);
        root.Options.Add(logFormat);

        root.SetAction(parseResult =>
        {
            if (parseResult.GetValue(quiet))
            {
                configuration = configuration with { Quiet = true };
            }

            int selectedVerbosity = parseResult.GetValue(diagnostic) ? 3 :
                parseResult.GetValue(veryVerbose) ? 2 :
                parseResult.GetValue(verbose) ? 1 : configuration.Verbosity;
            configuration = configuration with { Verbosity = selectedVerbosity };

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
        foreach (string? name in new[] { "resume", "status", "stop" })
        {
            root.Subcommands.Add(CreatePlaceholderCommand(name));
        }
        root.Subcommands.Add(CreateSmokeCommand(() => configuration));
        root.Subcommands.Add(CreateReportCommand());
        root.Subcommands.Add(CreateProfileCommand());

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
        var tasks = new Option<string>("--tasks")
        {
            Description = "Directory containing the dataset.toml or task.toml directories.",
            DefaultValueFactory = _ => Path.Combine(Directory.GetCurrentDirectory(), "tasks")
        };
        var verbose = new Option<bool>("--verbose") { Description = "Show run discovery and artifact details." };
        verbose.Aliases.Add("-v");
        command.Options.Add(manifest);
        command.Options.Add(dryRun);
        command.Options.Add(k);
        command.Options.Add(n);
        command.Options.Add(b);
        command.Options.Add(outputFormat);
        command.Options.Add(outputDirectory);
        command.Options.Add(model);
        command.Options.Add(effort);
        command.Options.Add(tasks);
        command.Options.Add(verbose);
        command.SetAction(parseResult =>
        {
            if (!parseResult.GetValue(dryRun))
            {
                try
                {
                    RunManifest runManifest = parseResult.GetValue(manifest) is { } manifestPath
                        ? RunManifestLoader.Load(manifestPath)
                        : new RunManifest();
                    RunConfiguration runConfiguration = RunConfigurationResolver.Resolve(
                        runManifest,
                        new RunConfigurationOverrides(
                            parseResult.GetValue(k),
                            parseResult.GetValue(n),
                            parseResult.GetValue(b),
                            parseResult.GetValue(outputFormat),
                            parseResult.GetValue(outputDirectory),
                            parseResult.GetValue(model),
                            parseResult.GetValue(effort)),
                        new Dictionary<string, string?>
                        {
                            ["WHARF_K"] = Environment.GetEnvironmentVariable("WHARF_K"),
                            ["WHARF_N"] = Environment.GetEnvironmentVariable("WHARF_N"),
                            ["WHARF_B"] = Environment.GetEnvironmentVariable("WHARF_B"),
                            ["WHARF_OUTPUT_FORMAT"] = Environment.GetEnvironmentVariable("WHARF_OUTPUT_FORMAT"),
                            ["WHARF_OUTPUT_DIRECTORY"] = Environment.GetEnvironmentVariable("WHARF_OUTPUT_DIRECTORY"),
                            ["WHARF_MODEL"] = Environment.GetEnvironmentVariable("WHARF_MODEL"),
                            ["WHARF_EFFORT"] = Environment.GetEnvironmentVariable("WHARF_EFFORT"),
                            ["WHARF_API_KEY"] = Environment.GetEnvironmentVariable("WHARF_API_KEY"),
                            ["WHARF_TOKEN"] = Environment.GetEnvironmentVariable("WHARF_TOKEN"),
                            ["WHARF_PASSWORD"] = Environment.GetEnvironmentVariable("WHARF_PASSWORD")
                        });
                    RunConfigurationValidator.Validate(runManifest, runConfiguration);
                    string? taskRoot = parseResult.GetValue(tasks);
                    if (string.IsNullOrWhiteSpace(taskRoot))
                        throw new RunException("run task directory is required");
                    WharfConfiguration loggerConfiguration = configuration with
                    {
                        Verbosity = Math.Max(configuration.Verbosity, parseResult.GetValue(verbose) ? 1 : 0)
                    };
                    using WharfLogger logger = WharfLogger.Create(loggerConfiguration);
                    RunResult result = new RunService().Execute(
                        taskRoot,
                        runConfiguration.OutputDirectory,
                        runConfiguration,
                        logger);
                    Console.WriteLine($"{result.Status}\t{result.TaskCount}\t{result.OutputDirectory}");
                    return ExitCode.Success.Value;
                }
                catch (RunException exception)
                {
                    Console.Error.WriteLine(exception.Message);
                    return ExitCode.InvalidInput.Value;
                }
                catch (RunConfigurationException exception)
                {
                    Console.Error.WriteLine(exception.Message);
                    return ExitCode.InvalidInput.Value;
                }
                catch (IOException exception)
                {
                    Console.Error.WriteLine($"unable to create run artifacts: {exception.Message}");
                    return ExitCode.InternalFailure.Value;
                }
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

    private static CliCommand CreateReportCommand()
    {
        var command = new CliCommand("report", "Render deterministic reports from retained evidence.");
        var input = new Option<string>("--input") { Description = "Path to a retained report input JSON file." };
        var outputDirectory = new Option<string>("--output-directory")
        {
            Description = "Directory for rendered report files.",
            DefaultValueFactory = _ => Directory.GetCurrentDirectory()
        };
        var format = new Option<string>("--format")
        {
            Description = "Report format: all, markdown, json, or jsonl.",
            DefaultValueFactory = _ => "all"
        };
        command.Options.Add(input);
        command.Options.Add(outputDirectory);
        command.Options.Add(format);
        command.SetAction(parseResult =>
        {
            try
            {
                string? inputPath = parseResult.GetValue(input);
                string? outputPath = parseResult.GetValue(outputDirectory);
                string? requestedFormat = parseResult.GetValue(format);
                if (inputPath is null || outputPath is null || requestedFormat is null)
                    throw new ReportInputException("report input, output directory, and format are required");

                ReportInput report = ReportRenderer.Load(inputPath);
                ReportRenderer renderer = new();
                string directory = outputPath;
                string selectedFormat = requestedFormat.ToLowerInvariant();
                if (selectedFormat is not ("all" or "markdown" or "json" or "jsonl"))
                    throw new ReportInputException("report format must be all, markdown, json, or jsonl");

                Directory.CreateDirectory(directory);
                if (selectedFormat is "all" or "markdown")
                    File.WriteAllText(Path.Combine(directory, "report.md"), renderer.RenderMarkdown(report));
                if (selectedFormat is "all" or "json")
                    File.WriteAllText(Path.Combine(directory, "report.json"), renderer.RenderJson(report) + Environment.NewLine);
                if (selectedFormat is "all" or "jsonl")
                    File.WriteAllText(Path.Combine(directory, "report.jsonl"), renderer.RenderJsonLines(report));
                return ExitCode.Success.Value;
            }
            catch (ReportInputException exception)
            {
                Console.Error.WriteLine(exception.Message);
                return ExitCode.InvalidInput.Value;
            }
            catch (IOException exception)
            {
                Console.Error.WriteLine($"unable to write report: {exception.Message}");
                return ExitCode.InternalFailure.Value;
            }
        });
        return command;
    }

    private static CliCommand CreateSmokeCommand(Func<WharfConfiguration> getConfiguration)
    {
        var command = new CliCommand("smoke", "Run one task through the run pipeline.");
        var taskRoot = new Option<string>("--tasks")
        {
            Description = "Directory containing smoke task.toml directories.",
            DefaultValueFactory = _ => Path.Combine(Directory.GetCurrentDirectory(), "smoke-tasks")
        };
        var outputDirectory = new Option<string>("--output-directory")
        {
            Description = "Directory for smoke artifacts and reports.",
            DefaultValueFactory = _ => Path.Combine(
                Directory.GetCurrentDirectory(),
                "jobs")
        };
        var allowUnsupported = new Option<bool>("--allow-unsupported-host")
        {
            Description = "Override platform classification only; Docker and capacity failures still block."
        };
        var json = new Option<bool>("--json") { Description = "Render the smoke result as JSON." };
        var verbose = new Option<bool>("--verbose") { Description = "Show smoke and run selection details." };
        verbose.Aliases.Add("-v");
        var veryVerbose = new Option<bool>("-vv") { Description = "Show task metadata and checksums." };
        var diagnostic = new Option<bool>("-vvv") { Description = "Show diagnostic task paths and resources." };
        var include = new Option<string?>("--include")
        {
            Description = "Select a smoke task by ID or dataset name."
        };
        include.Aliases.Add("-i");
        command.Options.Add(taskRoot);
        command.Options.Add(outputDirectory);
        command.Options.Add(allowUnsupported);
        command.Options.Add(json);
        command.Options.Add(verbose);
        command.Options.Add(veryVerbose);
        command.Options.Add(diagnostic);
        command.Options.Add(include);
        command.SetAction(parseResult =>
        {
            try
            {
                string? selectedTaskRoot = parseResult.GetValue(taskRoot);
                if (selectedTaskRoot is null)
                    throw new SmokeException("smoke task directory is required");
                HostCheckReport host = new HostCheckService(
                    [new PlatformProbe(), new DockerProbe(), new CapacityProbe()])
                    .Check(new HostCheckOptions(parseResult.GetValue(allowUnsupported)));
                string? output = parseResult.GetValue(outputDirectory);
                if (output is null)
                    throw new SmokeException("smoke output directory is required");
                int localVerbosity = parseResult.GetValue(diagnostic) ? 3 :
                    parseResult.GetValue(veryVerbose) ? 2 :
                    parseResult.GetValue(verbose) ? 1 : 0;
                WharfConfiguration loggerConfiguration = getConfiguration() with
                {
                    Verbosity = Math.Max(getConfiguration().Verbosity, localVerbosity)
                };
                using WharfLogger logger = WharfLogger.Create(loggerConfiguration);
                SmokeResult result = new SmokeService().Run(
                    selectedTaskRoot,
                    output,
                    host,
                    logger,
                    parseResult.GetValue(include));
                if (parseResult.GetValue(json))
                    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }));
                else
                    Console.WriteLine($"{result.Status}\t{result.TaskId}\t{result.OutputDirectory}");
                return result.ExitCode;
            }
            catch (SmokeException exception)
            {
                Console.Error.WriteLine(exception.Message);
                return ExitCode.InvalidInput.Value;
            }
            catch (IOException exception)
            {
                Console.Error.WriteLine($"unable to create smoke artifacts: {exception.Message}");
                return ExitCode.InternalFailure.Value;
            }
        });
        return command;
    }

    private static CliCommand CreateProfileCommand()
    {
        var command = new CliCommand("profile", "Recommend conservative host concurrency limits.");
        var profile = new Option<string>("--profile")
        {
            Description = "Published profile: on-prem-32 or on-prem-88.",
            DefaultValueFactory = _ => "on-prem-32"
        };
        var operatorLimit = new Option<int>("--operator-limit")
        {
            Description = "Maximum trial concurrency the operator permits.",
            DefaultValueFactory = _ => 1
        };
        var buildLimit = new Option<int>("--build-limit")
        {
            Description = "Maximum build concurrency the operator permits.",
            DefaultValueFactory = _ => 1
        };
        var json = new Option<bool>("--json");
        command.Options.Add(profile);
        command.Options.Add(operatorLimit);
        command.Options.Add(buildLimit);
        command.Options.Add(json);
        command.SetAction(parseResult =>
        {
            try
            {
                string? profileId = parseResult.GetValue(profile);
                if (profileId is null)
                    throw new QualificationException("profile is required");
                HostProfile selected = HostProfiles.Get(profileId);
                HostCapacity capacity = new(
                    Environment.ProcessorCount,
                    GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);
                ProfileRecommendation recommendation = new HostProfileService().Recommend(
                    selected,
                    capacity,
                    parseResult.GetValue(operatorLimit),
                    parseResult.GetValue(buildLimit));
                if (parseResult.GetValue(json))
                    Console.WriteLine(JsonSerializer.Serialize(recommendation, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }));
                else
                    Console.WriteLine($"{recommendation.ProfileId}\tN={recommendation.RecommendedConcurrency}\tB={recommendation.RecommendedBuildConcurrency}");
                return ExitCode.Success.Value;
            }
            catch (QualificationException exception)
            {
                Console.Error.WriteLine(exception.Message);
                return ExitCode.InvalidInput.Value;
            }
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
