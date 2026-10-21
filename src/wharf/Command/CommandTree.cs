using System.CommandLine;
using System.Text.Json;
using Wharf.Configuration;
using Wharf.Build;
using Wharf.Agent;
using Wharf.Copilot;
using Wharf.ExitCodes;
using Wharf.Host;
using Wharf.Logging;
using Wharf.Image;
using Wharf.Reporting;
using Wharf.Run;
using Wharf.Smoke;
using Wharf.Qualification;
using Wharf.Recovery;
using Wharf.Tasks;
using Wharf.Versioning;
using CliCommand = System.CommandLine.Command;

namespace Wharf.Command;

public static class CommandTree
{
    public static RootCommand Create(
        WharfConfiguration configuration,
        CancellationToken cancellationToken = default)
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

        root.Subcommands.Add(CreateVersionCommand());
        root.Subcommands.Add(CreateCompletionCommand());
        root.Subcommands.Add(CreateBuildCommand(() => configuration));
        root.Subcommands.Add(CreateRunCommand(configuration, logFormat, cancellationToken));
        root.Subcommands.Add(CreateResumeCommand(configuration, cancellationToken));
        root.Subcommands.Add(CreateProgressCommand());
        root.Subcommands.Add(CreateCheckCommand());
        foreach (string? name in new[] { "status", "stop" })
        {
            root.Subcommands.Add(CreatePlaceholderCommand(name));
        }
        root.Subcommands.Add(CreateSmokeCommand(() => configuration));
        root.Subcommands.Add(CreateReportCommand());
        root.Subcommands.Add(CreateAnalyticsCommand());
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

    private static CliCommand CreateBuildCommand(Func<WharfConfiguration> getConfiguration)
    {
        var command = new CliCommand("build", "Pre-build task images and retain Docker build cache.");
        var tasks = new Option<string>("--tasks")
        {
            Description = "Directory containing the dataset.toml or task.toml directories.",
            DefaultValueFactory = _ => Path.Combine(Directory.GetCurrentDirectory(), "tasks")
        };
        var verbose = new Option<bool>("--verbose") { Description = "Show image preparation details." };
        verbose.Aliases.Add("-v");
        command.Options.Add(tasks);
        command.Options.Add(verbose);
        command.SetAction(parseResult =>
        {
            try
            {
                WharfConfiguration configuration = getConfiguration() with
                {
                    Verbosity = Math.Max(
                        getConfiguration().Verbosity,
                        parseResult.GetValue(verbose) ? 1 : 0)
                };
                using WharfLogger logger = WharfLogger.Create(configuration);
                string? taskRoot = parseResult.GetValue(tasks);
                if (string.IsNullOrWhiteSpace(taskRoot))
                    throw new BuildException("build task directory is required");
                BuildResult result = new BuildService(
                    imagePreparer: new DockerAgentImagePreparer(progress: message => logger.Info(message))).Execute(
                    taskRoot,
                    logger);
                Console.WriteLine(
                    $"complete tasks={result.TaskCount} prepared={result.PreparedCount} " +
                    $"built={result.BuiltCount} " +
                    $"cache=retained output=none");
                return ExitCode.Success.Value;
            }
            catch (BuildException exception)
            {
                Console.Error.WriteLine(exception.Message);
                return ExitCode.InvalidInput.Value;
            }
            catch (AgentImageException exception)
            {
                Console.Error.WriteLine(exception.Message);
                return ExitCode.InternalFailure.Value;
            }
        });
        return command;
    }

    private static CliCommand CreateRunCommand(
        WharfConfiguration configuration,
        Option<string> logFormat,
        CancellationToken cancellationToken)
    {
        var command = new CliCommand("run", "Execute or preview a configured evaluation run.");
        var manifest = new Option<string?>("--manifest") { Description = "Path to a JSON run manifest." };
        var dryRun = new Option<bool>("--dry-run") { Description = "Validate and print a side-effect-free plan." };
        dryRun.Aliases.Add("-d");
        var k = new Option<int?>("-k") { Description = "Attempts per task." };
        var n = new Option<int?>("-n") { Description = "Task concurrency." };
        var p = new Option<int?>("-p") { Description = "Agent and verifier preparation concurrency." };
        var outputFormat = new Option<string?>("--output-format") { Description = "Output format: text or json." };
        var outputDirectory = new Option<string?>("--output-directory") { Description = "Relative output directory." };
        var logFile = new Option<string?>("--log-file")
        {
            Description = "JSONL progress log path. Defaults to run.jsonl inside the run directory."
        };
        var model = new Option<string?>("--model") { Description = "Model name." };
        model.Aliases.Add("-m");
        var effort = new Option<string?>("--effort")
        {
            Description = "Model effort: default, low, medium, high, or max."
        };
        effort.Aliases.Add("-e");
        var cliVersion = new Option<string?>("--cli-version")
        {
            Description = "Exact Copilot CLI version, or latest."
        };
        var retainWorkspaces = new Option<bool>("--retain-workspaces")
        {
            Description = "Retain terminal trial workspaces instead of deleting them after evidence capture."
        };
        var shortestFirst = new Option<bool>("--shortest-first")
        {
            Description = "Admit the shortest estimated tasks first within each K wave."
        };
        var include = new Option<string?>("--include")
        {
            Description = "Run only tasks matching the task ID, category, or language."
        };
        include.Aliases.Add("-i");
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
        command.Options.Add(p);
        command.Options.Add(outputFormat);
        command.Options.Add(outputDirectory);
        command.Options.Add(logFile);
        command.Options.Add(model);
        command.Options.Add(effort);
        command.Options.Add(cliVersion);
        command.Options.Add(retainWorkspaces);
        command.Options.Add(shortestFirst);
        command.Options.Add(include);
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
                            K: parseResult.GetValue(k),
                            N: parseResult.GetValue(n),
                            P: parseResult.GetValue(p),
                            OutputFormat: parseResult.GetValue(outputFormat),
                            OutputDirectory: parseResult.GetValue(outputDirectory),
                            Model: parseResult.GetValue(model),
                            Effort: parseResult.GetValue(effort),
                            CliVersion: parseResult.GetValue(cliVersion),
                            RetainWorkspaces: parseResult.GetValue(retainWorkspaces) ? true : null,
                            ShortestFirst: parseResult.GetValue(shortestFirst) ? true : null),
                        new Dictionary<string, string?>
                        {
                            ["K"] = Environment.GetEnvironmentVariable("K"),
                            ["N"] = Environment.GetEnvironmentVariable("N"),
                            ["P"] = Environment.GetEnvironmentVariable("P"),
                            ["WHARF_K"] = Environment.GetEnvironmentVariable("WHARF_K"),
                            ["WHARF_N"] = Environment.GetEnvironmentVariable("WHARF_N"),
                            ["WHARF_P"] = Environment.GetEnvironmentVariable("WHARF_P"),
                            ["WHARF_OUTPUT_FORMAT"] = Environment.GetEnvironmentVariable("WHARF_OUTPUT_FORMAT"),
                            ["WHARF_OUTPUT_DIRECTORY"] = Environment.GetEnvironmentVariable("WHARF_OUTPUT_DIRECTORY"),
                            ["MODEL"] = Environment.GetEnvironmentVariable("MODEL"),
                            ["WHARF_MODEL"] = Environment.GetEnvironmentVariable("WHARF_MODEL"),
                            ["WHARF_EFFORT"] = Environment.GetEnvironmentVariable("WHARF_EFFORT"),
                            ["WHARF_CLI_VERSION"] = Environment.GetEnvironmentVariable("WHARF_CLI_VERSION"),
                            ["WHARF_RETAIN_WORKSPACES"] =
                                Environment.GetEnvironmentVariable("WHARF_RETAIN_WORKSPACES"),
                            ["WHARF_API_KEY"] = Environment.GetEnvironmentVariable("WHARF_API_KEY"),
                            ["WHARF_TOKEN"] = Environment.GetEnvironmentVariable("WHARF_TOKEN"),
                            ["WHARF_PASSWORD"] = Environment.GetEnvironmentVariable("WHARF_PASSWORD")
                        });
                    RunConfigurationValidator.Validate(runManifest, runConfiguration);
                    string? taskRoot = parseResult.GetValue(tasks);
                    if (string.IsNullOrWhiteSpace(taskRoot))
                        throw new RunException("run task directory is required");
                    IReadOnlyList<HarborTask>? selectedTasks = null;
                    if (parseResult.GetValue(include) is { } includeExpression)
                    {
                        HarborTaskLoader loader = new();
                        selectedTasks = TaskSelector.Select(
                            loader.Load(taskRoot),
                            new TaskSelection(Include: includeExpression));
                        if (selectedTasks.Count == 0)
                        {
                            throw new RunException(
                                $"run task include did not match a task: {includeExpression}");
                        }
                    }
                    WharfConfiguration loggerConfiguration = configuration with
                    {
                        Verbosity = Math.Max(configuration.Verbosity, parseResult.GetValue(verbose) ? 1 : 0)
                    };
                    string? selectedLogFile = parseResult.GetValue(logFile);
                    using WharfLogger logger = selectedLogFile is null
                        ? WharfLogger.CreateDeferred(
                            loggerConfiguration,
                            Path.Combine(Directory.GetCurrentDirectory(), "run.jsonl"))
                        : WharfLogger.Create(loggerConfiguration, selectedLogFile);
                    RunResult result = new RunService(
                        imagePreparer: new DockerAgentImagePreparer(progress: message => logger.Info(message))).Execute(
                        taskRoot,
                        runConfiguration.OutputDirectory,
                        runConfiguration,
                        logger,
                        selectedTasks,
                        cancellationToken: cancellationToken);
                    Console.WriteLine(
                        $"complete runs={result.TaskCount} result=execution-pending " +
                        $"output={result.OutputDirectory} log={logger.JsonlPath}");
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
                catch (CopilotAdapterException exception)
                {
                    Console.Error.WriteLine(exception.Message);
                    return ExitCode.InternalFailure.Value;
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
                        K: parseResult.GetValue(k),
                        N: parseResult.GetValue(n),
                        P: parseResult.GetValue(p),
                        OutputFormat: parseResult.GetValue(outputFormat),
                        OutputDirectory: parseResult.GetValue(outputDirectory),
                        Model: parseResult.GetValue(model),
                        Effort: parseResult.GetValue(effort),
                        CliVersion: parseResult.GetValue(cliVersion),
                        RetainWorkspaces: parseResult.GetValue(retainWorkspaces) ? true : null,
                        ShortestFirst: parseResult.GetValue(shortestFirst) ? true : null),
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

    private static CliCommand CreateResumeCommand(
        WharfConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var command = new CliCommand(
            "resume",
            "Resume unfinished logical attempts in an existing run directory.");
        var runDirectory = new Option<string>("--run-directory")
        {
            Description = "Existing Wharf all-N run directory."
        };
        var tasks = new Option<string?>("--tasks")
        {
            Description = "Task root. Inferred from retained results when omitted."
        };
        var n = new Option<int?>("-n") { Description = "Override retained task concurrency." };
        var p = new Option<int?>("-p") { Description = "Override retained preparation concurrency." };
        var dryRun = new Option<bool>("--dry-run")
        {
            Description = "Validate and list the resume plan without running trials."
        };
        dryRun.Aliases.Add("-d");
        var verbose = new Option<bool>("--verbose")
        {
            Description = "Show resume execution details."
        };
        verbose.Aliases.Add("-v");
        command.Options.Add(runDirectory);
        command.Options.Add(tasks);
        command.Options.Add(n);
        command.Options.Add(p);
        command.Options.Add(dryRun);
        command.Options.Add(verbose);
        command.SetAction(parseResult =>
        {
            try
            {
                string? requestedRun = parseResult.GetValue(runDirectory);
                if (string.IsNullOrWhiteSpace(requestedRun))
                    throw new RecoveryException("resume run directory is required");

                RetainedResumePlan plan = new RetainedRunResumePlanner().Plan(
                    requestedRun,
                    parseResult.GetValue(tasks),
                    parseResult.GetValue(n),
                    parseResult.GetValue(p));
                Console.WriteLine(
                    $"resume completed={plan.CompletedCount} pending={plan.Attempts.Count} " +
                    $"K={plan.K} N={plan.N} P={plan.P} model={plan.Model} " +
                    $"effort={plan.Effort} cli={plan.CliVersion}");
                if (parseResult.GetValue(dryRun) || plan.Attempts.Count == 0)
                {
                    foreach (RetainedResumeAttempt attempt in plan.Attempts)
                    {
                        Console.WriteLine(
                            $"pending task={attempt.Attempt.Task.TaskId} " +
                            $"attempt={attempt.Attempt.AttemptNumber} " +
                            $"trial-retry={attempt.TrialRetry}");
                    }
                    return ExitCode.Success.Value;
                }

                RunConfiguration runConfiguration = plan.Configuration with
                {
                    N = plan.N,
                    P = Math.Min(plan.P, plan.N),
                    Credentials = new SecretSettings(
                        Environment.GetEnvironmentVariable("WHARF_API_KEY"),
                        Environment.GetEnvironmentVariable("WHARF_TOKEN"),
                        Environment.GetEnvironmentVariable("WHARF_PASSWORD"))
                };
                RunConfigurationValidator.Validate(null, runConfiguration);
                WharfConfiguration loggerConfiguration = configuration with
                {
                    Verbosity = Math.Max(
                        configuration.Verbosity,
                        parseResult.GetValue(verbose) ? 1 : 0)
                };
                using WharfLogger logger = WharfLogger.CreateDeferred(
                    loggerConfiguration,
                    Path.Combine(Directory.GetCurrentDirectory(), "run.jsonl"));
                IReadOnlyList<HarborTask> loadedTasks =
                    new HarborTaskLoader().Load(plan.TaskRoot);
                RunResult result = new RunService(
                    imagePreparer: new DockerAgentImagePreparer(
                        progress: message => logger.Info(message))).Execute(
                    plan.TaskRoot,
                    "jobs",
                    runConfiguration,
                    logger,
                    loadedTasks,
                    cancellationToken: cancellationToken,
                    resume: new RunResumeTarget(plan.RunDirectory, plan.Attempts));
                Console.WriteLine(
                    $"resume complete trials={plan.Attempts.Count} " +
                    $"run={result.AttemptDirectory} log={logger.JsonlPath}");
                return ExitCode.Success.Value;
            }
            catch (Exception exception) when (
                exception is RecoveryException or
                RunException or
                RunConfigurationException or
                CopilotAdapterException or
                IOException)
            {
                Console.Error.WriteLine(exception.Message);
                return ExitCode.InvalidInput.Value;
            }
        });
        return command;
    }

    private static CliCommand CreateProgressCommand()
    {
        var command = new CliCommand(
            "progress",
            "Summarize an executing or completed run from its JSONL log.");
        var logFile = new Option<string>("--log-file")
        {
            Description = "JSONL progress log path.",
            DefaultValueFactory = _ => "run.jsonl"
        };
        var json = new Option<bool>("--json") { Description = "Render the summary as JSON." };
        var verbose = new Option<bool>("--verbose")
        {
            Description = "List active trials by descending stage duration."
        };
        verbose.Aliases.Add("-v");
        command.Options.Add(logFile);
        command.Options.Add(json);
        command.Options.Add(verbose);
        command.SetAction(parseResult =>
        {
            try
            {
                string? path = parseResult.GetValue(logFile);
                if (string.IsNullOrWhiteSpace(path))
                {
                    Console.Error.WriteLine("progress log file is required");
                    return ExitCode.InvalidInput.Value;
                }

                RunProgress progress = ProgressService.Read(path);
                if (parseResult.GetValue(json))
                {
                    Console.WriteLine(JsonSerializer.Serialize(progress, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }));
                }
                else
                {
                    foreach (string line in FormatProgress(
                                 progress,
                                 parseResult.GetValue(verbose)))
                        Console.WriteLine(line);
                }

                return ExitCode.Success.Value;
            }
            catch (FileNotFoundException exception)
            {
                Console.Error.WriteLine(exception.Message);
                return ExitCode.InvalidInput.Value;
            }
        });
        return command;
    }

    internal static IReadOnlyList<string> FormatProgress(
        RunProgress progress,
        bool verbose = false)
    {
        var lines = new List<string>
        {
            (
                        $"completed={progress.TestsCompleted} pass-rate={FormatPercent(progress.PassPercent)} " +
                        $"pass@={progress.PassAtTotal} failed={progress.FailedTests} " +
                        $"errors={progress.Errors} timeouts={progress.Timeouts} " +
                        $"retries={progress.Retries} missing-grader={progress.MissingGraderResults}"),
            (
                        $"queued={progress.Queued} running={progress.TestsRunning} building={progress.Building} " +
                        $"agent={progress.Agents} verifier={progress.Verifiers} cleanup={progress.Cleanup}"),
            (
                        $"avg-cost={FormatCost(progress.AverageCost)} " +
                        $"total-cost={FormatCost(progress.TotalCost)}")
        };
        if (progress.ByK.Keys.Any(k => k > 1))
        {
            foreach ((int k, ProgressGroup group) in progress.ByK.OrderBy(item => item.Key))
            {
                lines.Add(
                            $"k={k} completed={group.TestsCompleted} " +
                            $"pass-rate={FormatPercent(group.PassPercent)} " +
                            $"pass@={group.PassAtTotal} failed={group.FailedTests} " +
                            $"errors={group.Errors} timeouts={group.Timeouts}");
                lines.Add(
                            $"k={k} avg-cost={FormatCost(group.AverageCost)} " +
                            $"total-cost={FormatCost(group.TotalCost)}");
            }
        }

        foreach ((string type, int count) in progress.ErrorTypes.OrderBy(item => item.Key))
            lines.Add($"error-type={type} count={count}");
        foreach ((string type, int count) in
                 (progress.MissingGraderTypes ?? new Dictionary<string, int>())
                 .OrderBy(item => item.Key))
        {
            lines.Add($"missing-grader-type={type} count={count}");
        }
        foreach (PreparationWaitAggregate wait in
                 (progress.PreparationWaits ?? [])
                 .OrderBy(item => item.K)
                 .ThenBy(item => item.Operation, StringComparer.Ordinal))
        {
            lines.Add(
                $"queue-wait operation={wait.Operation} k={wait.K} count={wait.Count} " +
                $"avg={wait.AverageWaitMilliseconds:0.##}ms " +
                $"max={wait.MaximumWaitMilliseconds}ms total={wait.TotalWaitMilliseconds}ms");
        }
        if (verbose && progress.ActiveTrials.Count > 0)
        {
            lines.Add(string.Empty);
            int stageWidth = progress.ActiveTrials.Count == 0
                ? 0
                : progress.ActiveTrials.Max(trial => trial.Stage.Length);
            foreach (ActiveTrialProgress trial in progress.ActiveTrials)
            {
                lines.Add(
                    $"{WharfLogger.FormatElapsed(
                        TimeSpan.FromMilliseconds(trial.DurationMilliseconds))} " +
                    $"{trial.Stage.PadRight(stageWidth)} {trial.Name}");
            }
        }

        return lines;
    }

    private static string FormatCost(decimal? cost) =>
        cost?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) ?? "unknown";

    private static string FormatPercent(decimal? value) =>
        value.HasValue
            ? value.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "%"
            : "unknown";

    private static CliCommand CreateVersionCommand()
    {
        var command = new CliCommand("version", "Print the Wharf version.");
        command.SetAction(_ =>
        {
            Console.WriteLine(WharfVersion.Current);
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

    private static CliCommand CreateAnalyticsCommand()
    {
        var command = new CliCommand(
            "analytics",
            "Export portable trial analytics or rebuild a data-only manifest.");
        command.Subcommands.Add(CreateAnalyticsExportCommand());
        command.Subcommands.Add(CreateAnalyticsManifestCommand());
        return command;
    }

    private static CliCommand CreateAnalyticsExportCommand()
    {
        var command = new CliCommand(
            "export",
            "Export one canonical JSONL row per retained logical trial.");
        var runDirectory = new Option<string>("--run-directory")
        {
            Description = "Completed Wharf run directory."
        };
        var output = new Option<string>("--output")
        {
            Description = "Analytics JSONL output path."
        };
        var arm = new Option<string>("--arm") { Description = "Canonical benchmark arm name." };
        var agent = new Option<string>("--agent")
        {
            Description = "Agent product name.",
            DefaultValueFactory = _ => "copilot-cli"
        };
        var agentVersion = new Option<string?>("--agent-version")
        {
            Description = "Effective agent version."
        };
        var model = new Option<string>("--model") { Description = "Effective model name." };
        model.Aliases.Add("-m");
        var effort = new Option<string>("--effort")
        {
            Description = "Effective reasoning effort.",
            DefaultValueFactory = _ => "max"
        };
        effort.Aliases.Add("-e");
        var subagents = new Option<bool>("--subagents")
        {
            Description = "Whether subagent tools were available.",
            DefaultValueFactory = _ => true
        };
        var job = new Option<string?>("--job") { Description = "Optional batch or job name." };
        command.Options.Add(runDirectory);
        command.Options.Add(output);
        command.Options.Add(arm);
        command.Options.Add(agent);
        command.Options.Add(agentVersion);
        command.Options.Add(model);
        command.Options.Add(effort);
        command.Options.Add(subagents);
        command.Options.Add(job);
        command.SetAction(parseResult =>
        {
            try
            {
                string runPath = RequiredOption(parseResult.GetValue(runDirectory), "run directory");
                string outputPath = RequiredOption(parseResult.GetValue(output), "analytics output");
                var options = new AnalyticsExportOptions(
                    RequiredOption(parseResult.GetValue(arm), "analytics arm"),
                    RequiredOption(parseResult.GetValue(agent), "agent"),
                    parseResult.GetValue(agentVersion),
                    RequiredOption(parseResult.GetValue(model), "model"),
                    RequiredOption(parseResult.GetValue(effort), "effort"),
                    parseResult.GetValue(subagents),
                    parseResult.GetValue(job));
                IReadOnlyList<AnalyticsTrial> rows =
                    new AnalyticsExporter().Export(runPath, outputPath, options);
                Console.WriteLine(
                    $"exported trials={rows.Count} tasks={rows.Select(row => row.TaskId).Distinct().Count()} " +
                    $"output={Path.GetFullPath(outputPath)}");
                return ExitCode.Success.Value;
            }
            catch (AnalyticsEvidenceException exception)
            {
                Console.Error.WriteLine(exception.Message);
                return ExitCode.InvalidInput.Value;
            }
            catch (IOException exception)
            {
                Console.Error.WriteLine($"unable to export analytics: {exception.Message}");
                return ExitCode.InternalFailure.Value;
            }
        });
        return command;
    }

    private static CliCommand CreateAnalyticsManifestCommand()
    {
        var command = new CliCommand(
            "manifest",
            "Rebuild manifest.json strictly from analytics JSONL files.");
        var dataDirectory = new Option<string>("--data-directory")
        {
            Description = "Directory containing canonical analytics JSONL files.",
            DefaultValueFactory = _ => Path.Combine(Directory.GetCurrentDirectory(), "data")
        };
        var output = new Option<string?>("--output")
        {
            Description = "Manifest output path. Defaults to <data-directory>/manifest.json."
        };
        command.Options.Add(dataDirectory);
        command.Options.Add(output);
        command.SetAction(parseResult =>
        {
            try
            {
                string directory = RequiredOption(
                    parseResult.GetValue(dataDirectory),
                    "analytics data directory");
                IReadOnlyList<DataManifestEntry> entries =
                    new DataManifestBuilder().Build(directory, parseResult.GetValue(output));
                Console.WriteLine(
                    $"manifest arms={entries.Count} trials={entries.Sum(entry => entry.Trials)} " +
                    $"data={Path.GetFullPath(directory)}");
                return ExitCode.Success.Value;
            }
            catch (AnalyticsEvidenceException exception)
            {
                Console.Error.WriteLine(exception.Message);
                return ExitCode.InvalidInput.Value;
            }
            catch (IOException exception)
            {
                Console.Error.WriteLine($"unable to build analytics manifest: {exception.Message}");
                return ExitCode.InternalFailure.Value;
            }
        });
        return command;
    }

    private static string RequiredOption(string? value, string name) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new AnalyticsEvidenceException($"{name} is required");

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
                    Console.WriteLine(
                        $"complete task={result.TaskId} result={result.Status} output={result.OutputDirectory}");
                return result.ExitCode;
            }
            catch (SmokeException exception)
            {
                Console.Error.WriteLine(exception.Message);
                return ExitCode.InvalidInput.Value;
            }
            catch (CopilotAdapterException exception)
            {
                Console.Error.WriteLine(exception.Message);
                return ExitCode.InternalFailure.Value;
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
        var preparationLimit = new Option<int>("--preparation-limit")
        {
            Description = "Maximum preparation concurrency the operator permits.",
            DefaultValueFactory = _ => 1
        };
        var json = new Option<bool>("--json");
        command.Options.Add(profile);
        command.Options.Add(operatorLimit);
        command.Options.Add(preparationLimit);
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
                    parseResult.GetValue(preparationLimit));
                if (parseResult.GetValue(json))
                    Console.WriteLine(JsonSerializer.Serialize(recommendation, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }));
                else
                    Console.WriteLine(
                        $"{recommendation.ProfileId}\tN={recommendation.RecommendedConcurrency}\t" +
                        $"P={recommendation.RecommendedPreparationConcurrency}");
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
