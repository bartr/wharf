using System.Text.Json;
using System.Text.RegularExpressions;
using Wharf.Configuration;
using Wharf.Recovery;
using Wharf.Run;

namespace Wharf.Reporting;

public sealed record AnalyticsDiscoveryOptions(
    string RunDirectory = "jobs",
    string? OutputDirectory = null,
    bool Recompute = false,
    string? Output = null,
    string? Arm = null,
    string? Agent = null,
    string? AgentVersion = null,
    string? Model = null,
    string? Effort = null,
    bool? Subagents = null,
    string? Job = null);

public sealed class AnalyticsExportDiscovery
{
    public int Export(AnalyticsDiscoveryOptions options, Action<string> progress)
    {
        if (string.IsNullOrWhiteSpace(options.RunDirectory) ||
            options.Output is not null && string.IsNullOrWhiteSpace(options.Output) ||
            options.OutputDirectory is not null && string.IsNullOrWhiteSpace(options.OutputDirectory))
            throw new AnalyticsEvidenceException("analytics directory and output paths must not be empty");
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.RunDirectory));
        if (!Directory.Exists(root))
            throw new AnalyticsEvidenceException($"run evidence directory does not exist: {root}");

        string[] runs = Discover(root).ToArray();
        if (runs.Select(JobsTree).Distinct(StringComparer.Ordinal).Count() > 1)
            throw new AnalyticsEvidenceException("discovery spans multiple jobs trees; select one suite's jobs directory");
        var groups = runs
            .GroupBy(path => Directory.GetParent(path)!.Name, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();
        if (options.Output is not null && groups.Length != 1)
            throw new AnalyticsEvidenceException("--output requires exactly one discovered arm; use --output-directory for multiple arms");
        string data = Path.GetFullPath(options.OutputDirectory ?? DefaultDataDirectory(runs.FirstOrDefault() ?? root));
        var exports = new List<(string Arm, string Output, int Runs, IReadOnlyList<AnalyticsTrial> Rows)>();
        foreach (var group in groups)
        {
            CheckOverride("arm", options.Arm, group.Key);
            string output = options.Output is null
                ? Path.Combine(data, $"{group.Key}.jsonl")
                : Path.GetFullPath(options.Output);
            if (!options.Recompute && File.Exists(output))
            {
                progress($"skipped arm={group.Key} reason=export-exists output={output}");
                continue;
            }

            var rows = new List<AnalyticsTrial>();
            int completedRuns = 0;
            foreach (string run in group.Order(StringComparer.Ordinal))
            {
                if (WriterUnavailable(run))
                {
                    progress($"skipped run={run} reason=run-writer-unavailable");
                    continue;
                }
                RetainedRunMetadata? metadata = RetainedRunMetadataStore.Read(run);
                if (metadata?.Configuration is { NoOp: true } or { Oracle: true })
                {
                    progress($"skipped run={run} reason=baseline");
                    continue;
                }
                string log = Path.Combine(run, "run.jsonl");
                if (!File.Exists(log))
                {
                    progress($"skipped run={run} reason=missing-run-log");
                    continue;
                }
                Match plan = RetainedRunResumePlanner.ReadPlanMetadata(log);
                int k = Positive(plan, "k");
                int tasks = Positive(plan, "tasks");
                if (tasks > int.MaxValue / k)
                    throw new AnalyticsEvidenceException($"retained attempt count is too large: {run}");
                int expected = k * tasks;
                if (Positive(plan, "runs") != expected)
                    throw new AnalyticsEvidenceException($"inconsistent retained attempt count: {run}");
                if (metadata is not null &&
                    (metadata.Configuration.K != k || metadata.Tasks.Count != tasks))
                    throw new AnalyticsEvidenceException($"run plan and log disagree: {run}");
                if (metadata is not null && metadata.Tasks.Any(task =>
                        string.IsNullOrWhiteSpace(task.TaskId) || string.IsNullOrWhiteSpace(task.SourceChecksum) ||
                        task.SourceChecksum.Length < 8))
                    throw new AnalyticsEvidenceException($"invalid retained task identity: {run}");

                BatchState batch = RunBatch.Read(run);
                if (batch.Invocations.LastOrDefault() is { } invocation &&
                    (invocation.Status != "completed" || invocation.Pending != 0))
                {
                    progress($"skipped run={run} reason=needs-resume status={invocation.Status}");
                    continue;
                }
                if (batch.Rejections.Any(rejection => !rejection.Deleted))
                {
                    progress($"skipped run={run} reason=needs-resume pending-quota-rejection");
                    continue;
                }
                if (!Complete(run, metadata, k, tasks))
                {
                    progress($"skipped run={run} reason=incomplete-or-needs-resume");
                    continue;
                }

                Match arm = RetainedRunResumePlanner.ParseArmName(group.Key);
                if (!arm.Success)
                    throw new AnalyticsEvidenceException($"cannot infer CLI/model/effort/version from arm: {group.Key}");
                if (metadata is null)
                    progress($"warning run={run} reason=legacy-metadata-inferred-from-arm-and-log");
                string cli = metadata?.Configuration.Cli ?? arm.Groups["cli"].Value;
                string model = metadata?.Configuration.Model ?? arm.Groups["model"].Value;
                string effort = metadata?.Configuration.Effort ?? arm.Groups["effort"].Value;
                if (cli == "copilot")
                    effort = RunConfigurationResolver.ResolveEffectiveEffort(model, effort);
                string version = metadata?.Configuration.CliVersion ?? arm.Groups["version"].Value;
                if (version == "latest")
                {
                    version = arm.Groups["version"].Value;
                    progress($"warning run={run} requested-cli-version=latest effective-cli-version={version}");
                }
                if (RunService.BuildJobDirectoryName(model, effort, version, version, cli) != group.Key)
                    throw new AnalyticsEvidenceException($"arm directory contradicts retained configuration: {run}");
                string agent = cli == "copilot" ? "copilot-cli" : cli;
                CheckOverride("agent", options.Agent, agent);
                CheckOverride("agent-version", options.AgentVersion, version);
                CheckOverride("model", options.Model, model);
                CheckOverride("effort", options.Effort, effort);
                CheckOverride("job", options.Job, Path.GetFileName(run));
                var labels = new AnalyticsExportOptions(
                    group.Key, agent, version, model, effort, options.Subagents,
                    options.Job ?? Path.GetFileName(run));
                IReadOnlyList<AnalyticsTrial> trials = new RunEvidenceReader().Read(run, labels);
                if (trials.Count != expected)
                    throw new AnalyticsEvidenceException($"exported trial count does not match retained plan: {run}");
                foreach (AnalyticsTrial trial in trials)
                {
                    string? checksum = metadata?.Tasks.Single(identity => identity.TaskId == trial.TaskId).SourceChecksum;
                    if (metadata is null && trial.TaskChecksum is { Length: >= 8 } legacyChecksum &&
                        RetainedRunResumePlanner.TrialDirectoryName(trial.TaskId, legacyChecksum,
                            Attempt(run, trial.Trial), k) == trial.Trial)
                        checksum = legacyChecksum;
                    rows.Add(trial with { TaskChecksum = checksum });
                }
                completedRuns++;
                progress($"included run={run} trials={trials.Count} subagents={options.Subagents?.ToString() ?? "unknown"}");
            }
            if (completedRuns == 0)
            {
                progress($"skipped arm={group.Key} reason=no-completed-runs");
                continue;
            }
            AnalyticsTrial[] ordered = rows.OrderBy(row => row.Job, StringComparer.Ordinal)
                .ThenBy(row => row.Trial, StringComparer.Ordinal).ToArray();
            int missingChecksums = ordered.Count(row => row.TaskChecksum is null);
            if (missingChecksums > 0)
                progress($"warning arm={group.Key} missing-task-checksums={missingChecksums}");
            if (ordered.Select(row => (row.Job, row.Trial)).Distinct().Count() != ordered.Length)
                throw new AnalyticsEvidenceException($"duplicate job/trial identities in arm: {group.Key}");
            exports.Add((group.Key, output, completedRuns, ordered));
        }

        int exported = 0;
        foreach (var export in exports)
        {
            progress($"exporting arm={export.Arm} runs={export.Runs} trials={export.Rows.Count}");
            if (AnalyticsExporter.Write(export.Output, export.Rows, options.Recompute))
            {
                exported += export.Rows.Count;
                progress($"exported arm={export.Arm} trials={export.Rows.Count} output={export.Output}");
            }
            else
                progress($"skipped arm={export.Arm} reason=export-exists output={export.Output}");
        }
        if (groups.Length == 0)
            progress($"skipped directory={root} reason=no-all-N-runs");
        return exported;
    }

    private static IEnumerable<string> Discover(string root)
    {
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            yield break;
        if (Regex.IsMatch(Path.GetFileName(root), @"^all-[1-9]\d*$", RegexOptions.CultureInvariant))
        {
            yield return root;
            yield break;
        }
        foreach (string directory in Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal))
            foreach (string run in Discover(directory))
                yield return run;
    }

    private static string DefaultDataDirectory(string root)
    {
        for (DirectoryInfo? directory = new(root); directory is not null; directory = directory.Parent)
            if (directory.Name == "jobs" && directory.Parent is not null)
                return Path.Combine(directory.Parent.FullName, "data");
        return Path.Combine(Directory.GetCurrentDirectory(), "data");
    }

    private static string JobsTree(string run)
    {
        DirectoryInfo? parent = Directory.GetParent(run);
        for (DirectoryInfo? directory = parent; directory is not null; directory = directory.Parent)
            if (directory.Name == "jobs")
                return directory.FullName;
        return parent?.Parent?.FullName ?? parent?.FullName ?? run;
    }

    private static bool WriterUnavailable(string run)
    {
        string path = Path.Combine(run, ".writer.lock");
        if (!File.Exists(path))
            return false;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return false;
        }
        catch (IOException) when (File.Exists(path))
        {
            return true;
        }
    }

    private static bool Complete(string run, RetainedRunMetadata? metadata, int k, int tasks)
    {
        var identities = new List<(string Task, int Attempt)>();
        foreach (string directory in Directory.EnumerateDirectories(run))
        {
            string result = Path.Combine(directory, "result.json");
            if (!File.Exists(result))
                continue;
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(result));
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("taskId", out JsonElement id) || id.ValueKind != JsonValueKind.String ||
                    !root.TryGetProperty("attempt", out JsonElement number) || number.ValueKind != JsonValueKind.Number ||
                    !number.TryGetInt32(out int attempt) || string.IsNullOrWhiteSpace(id.GetString()))
                    return false;
                string taskId = id.GetString()!;
                if (!RetainedRunResumePlanner.IsValidTerminalResult(result, taskId, attempt) ||
                    root.GetProperty("status").GetString() is "created" or "pending" or "running" or "interrupted" or "quota-paused")
                    return false;
                RetainedTaskIdentity? retained = metadata?.Tasks.SingleOrDefault(task => task.TaskId == taskId);
                if (metadata is not null &&
                    (retained is null || retained.SourceChecksum.Length < 8 ||
                     RetainedRunResumePlanner.TrialDirectoryName(taskId, retained.SourceChecksum, attempt, k) != Path.GetFileName(directory)))
                    return false;
                if (retained is not null && root.TryGetProperty("taskChecksum", out JsonElement checksum) &&
                    (checksum.ValueKind != JsonValueKind.String || checksum.GetString() != retained.SourceChecksum))
                    return false;
                if (metadata is null)
                {
                    Match suffix = Regex.Match(Path.GetFileName(directory),
                        @"__(?<checksum>[0-9a-fA-F]{8})(?:__attempt-\d+)?$", RegexOptions.CultureInvariant);
                    if (!suffix.Success ||
                        RetainedRunResumePlanner.TrialDirectoryName(taskId,
                            suffix.Groups["checksum"].Value, attempt, k) != Path.GetFileName(directory))
                        return false;
                }
                identities.Add((taskId, attempt));
            }
            catch (JsonException)
            {
                return false;
            }
        }
        return identities.Count == checked(k * tasks) &&
            identities.Distinct().Count() == identities.Count &&
            identities.GroupBy(identity => identity.Task).Count() == tasks &&
            identities.GroupBy(identity => identity.Task).All(group =>
                group.Select(identity => identity.Attempt).Order().SequenceEqual(Enumerable.Range(1, k)));
    }

    private static int Attempt(string run, string trial)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(run, trial, "result.json")));
        return document.RootElement.GetProperty("attempt").GetInt32();
    }

    private static int Positive(Match plan, string name) =>
        int.TryParse(plan.Groups[name].Value, out int value) && value > 0
            ? value
            : throw new AnalyticsEvidenceException($"invalid retained plan value: {name}");

    private static void CheckOverride(string name, string? supplied, string retained)
    {
        if (supplied is not null && !string.Equals(supplied, retained, StringComparison.Ordinal))
            throw new AnalyticsEvidenceException($"--{name} contradicts retained evidence: supplied={supplied}, retained={retained}");
    }
}
