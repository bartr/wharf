using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using Wharf.Agent;
using Wharf.Configuration;
using Wharf.Recovery;
using Wharf.State;
using Wharf.Tasks;

namespace Wharf.Run;

internal sealed record BatchInvocation(
    string Id,
    int? Requested,
    DateTimeOffset Started,
    string Status,
    IReadOnlyList<string> Selected,
    IReadOnlyList<string> Admitted,
    IReadOnlyList<string> AgentStarted,
    IReadOnlyList<string> Accepted,
    DateTimeOffset? Finished = null,
    AgentQuotaFailure? Quota = null,
    int Pending = 0);

internal sealed record QuotaRejection(
    string Id,
    string TrialDirectory,
    string TaskId,
    int Attempt,
    DateTimeOffset Cutoff,
    string AuditPath,
    bool Deleted,
    decimal? KnownCredits,
    long? KnownAgentMilliseconds,
    decimal? KnownCostUsd = null);

internal sealed record BatchState(
    string SchemaVersion,
    IReadOnlyList<BatchInvocation> Invocations,
    IReadOnlyList<QuotaRejection> Rejections);

internal sealed class RunBatch : IDisposable
{
    internal const string FileName = "batches.json";
    private readonly object sync = new();
    private readonly string run;
    private readonly FileStream writer;
    private BatchState state;
    private BatchInvocation? current;
    private AgentQuotaFailure? quota;

    public RunBatch(string runDirectory)
    {
        run = Path.GetFullPath(runDirectory);
        try
        {
            writer = new FileStream(
                Path.Combine(run, ".writer.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
        }

        catch (IOException exception)
        {
            throw new RunException($"run writer is unavailable: {run}: {exception.Message}");
        }
        try
        {
            state = Read(run);
            RecoverDeletions();
            state = state with
            {
                Invocations = state.Invocations.Select(batch => batch.Status == "running"
                    ? batch with { Status = "interrupted", Finished = DateTimeOffset.UtcNow }
                    : batch).ToArray()
            };
        }
        catch
        {
            writer.Dispose();
            throw;
        }
    }

    public static void ValidateSize(int? size)
    {
        if (size is <= 0)
            throw new RunException("batch size must be a positive integer");
    }

    public static BatchState Read(string runDirectory)
    {
        string path = Path.Combine(runDirectory, FileName);
        if (!File.Exists(path))
            return new BatchState("1.0", [], []);
        try
        {
            BatchState state = JsonSerializer.Deserialize<BatchState>(
                File.ReadAllText(path), StateJson.Options)
                ?? throw new RunException($"batch state is null: {path}");
            if (state.SchemaVersion != "1.0" || state.Invocations is null ||
                state.Rejections is null ||
                state.Invocations.Any(batch => batch is null ||
                    string.IsNullOrWhiteSpace(batch.Id) || batch.Requested is <= 0 ||
                    batch.Selected is null || batch.Admitted is null ||
                    batch.AgentStarted is null || batch.Accepted is null ||
                    !ValidIds(batch.Selected) || !ValidIds(batch.Admitted) ||
                    !ValidIds(batch.AgentStarted) || !ValidIds(batch.Accepted) ||
                    batch.Admitted.Except(batch.Selected, StringComparer.Ordinal).Any() ||
                    batch.AgentStarted.Except(batch.Admitted, StringComparer.Ordinal).Any() ||
                    batch.Accepted.Except(batch.Admitted, StringComparer.Ordinal).Any() ||
                    (batch.Requested.HasValue && batch.Selected.Count > batch.Requested.Value) ||
                    batch.Pending < 0 ||
                    batch.Status is not ("running" or "interrupted" or "completed" or
                        "batch-limit" or "quota-paused" or "cancelled" or "failed")) ||
                state.Invocations.Select(batch => batch.Id).Distinct().Count() != state.Invocations.Count ||
                state.Rejections.Any(item => item is null ||
                    string.IsNullOrWhiteSpace(item.Id) || Path.GetFileName(item.Id) != item.Id ||
                    item.Attempt < 1 || item.Cutoff == default ||
                    string.IsNullOrWhiteSpace(item.TaskId) || item.KnownAgentMilliseconds < 0 ||
                    item.KnownCredits < 0 ||
                    string.IsNullOrWhiteSpace(item.TrialDirectory) ||
                    Path.GetFileName(item.TrialDirectory) != item.TrialDirectory ||
                    string.IsNullOrWhiteSpace(item.AuditPath)) ||
                state.Rejections.Select(item => item.Id).Distinct().Count() != state.Rejections.Count)
                throw new RunException($"invalid batch state: {path}");
            if (RetainedRunMetadataStore.Read(runDirectory) is { } retained)
            {
                var ids = retained.Tasks.SelectMany(task =>
                    Enumerable.Range(1, retained.Configuration.K)
                        .Select(attempt => $"{task.TaskId}#attempt-{attempt}"))
                    .ToHashSet(StringComparer.Ordinal);
                if (state.Invocations.Any(batch => batch.Selected.Any(id => !ids.Contains(id))) ||
                    state.Rejections.Any(item => item.Attempt > retained.Configuration.K ||
                        !retained.Tasks.Any(task => task.TaskId == item.TaskId &&
                            RetainedRunResumePlanner.TrialDirectoryName(
                                task.TaskId, task.SourceChecksum, item.Attempt, retained.Configuration.K) ==
                            item.TrialDirectory)))
                    throw new RunException($"batch state does not match retained logical identities: {path}");
            }
            return state;
        }

        catch (JsonException exception)
        {
            throw new RunException($"malformed batch state: {path}: {exception.Message}");
        }
    }

    private static bool ValidIds(IReadOnlyList<string> values) =>
        values.All(value => !string.IsNullOrWhiteSpace(value)) &&
        values.Distinct(StringComparer.Ordinal).Count() == values.Count;

    public void Begin(int? requested, IReadOnlyList<TaskAttempt> selected, int? pending = null)
    {
        ValidateSize(requested);
        lock (sync)
        {
            current = new BatchInvocation(
                Guid.NewGuid().ToString("N"), requested, DateTimeOffset.UtcNow, "running",
                selected.Select(item => item.AttemptId).ToArray(), [], [], [],
                Pending: pending ?? selected.Count);
            state = state with { Invocations = state.Invocations.Append(current).ToArray() };
            Save();
        }
    }

    public bool Admit(string id)
    {
        lock (sync)
        {
            if (quota is not null)
                return false;
            BatchInvocation batch = Current();
            if (!batch.Selected.Contains(id, StringComparer.Ordinal) ||
                batch.Admitted.Contains(id, StringComparer.Ordinal))
                throw new RunException($"invalid batch admission: {id}");
            Update(batch with { Admitted = batch.Admitted.Append(id).ToArray() });
            return true;
        }
    }

    public void AgentStarting(string id)
    {
        lock (sync)
        {
            if (quota is not null)
                throw new AgentExecutionException("run paused after quota exhaustion", "QuotaPaused");
            BatchInvocation batch = Current();
            if (!batch.Admitted.Contains(id, StringComparer.Ordinal))
                throw new RunException($"agent start without admission: {id}");
            if (!batch.AgentStarted.Contains(id, StringComparer.Ordinal))
                Update(batch with { AgentStarted = batch.AgentStarted.Append(id).ToArray() });
        }
    }

    public void Stop(AgentQuotaFailure failure)
    {
        lock (sync)
        {
            quota ??= failure;
            Update(Current() with { Quota = quota });
        }
    }

    public void Stop(
        AgentQuotaFailure failure, string trialDirectory, TaskAttempt attempt,
        RunConfiguration configuration)
    {
        lock (sync)
        {
            quota ??= failure;
            Update(Current() with { Quota = quota }, save: false);
            RecordRejection(trialDirectory, attempt, configuration, failure, delete: false);
        }
    }

    public bool IsStopped
    {
        get { lock (sync) return quota is not null; }
    }

    public BatchInvocation Summary
    {
        get { lock (sync) return Current(); }
    }

    public int RejectedCount
    {
        get { lock (sync) return state.Rejections.Count(item => item.Cutoff >= Current().Started); }
    }

    public void Accept(string id)
    {
        lock (sync)
        {
            BatchInvocation batch = Current();
            if (!batch.Admitted.Contains(id, StringComparer.Ordinal) ||
                batch.Accepted.Contains(id, StringComparer.Ordinal) || batch.Pending <= 0)
                throw new RunException($"invalid batch acceptance: {id}");
            Update(batch with
            {
                Accepted = batch.Accepted.Append(id).ToArray(),
                Pending = batch.Pending - 1
            });
        }
    }

    public void Reject(
        string trialDirectory,
        TaskAttempt attempt,
        RunConfiguration configuration,
        AgentQuotaFailure? failure)
    {
        lock (sync)
        {
            RecordRejection(trialDirectory, attempt, configuration, failure, delete: true);
        }
    }

    private void RecordRejection(
        string trialDirectory, TaskAttempt attempt, RunConfiguration configuration,
        AgentQuotaFailure? failure, bool delete)
    {
        string directory = ValidateTrialPath(trialDirectory);
        QuotaRejection? previous = state.Rejections.LastOrDefault(item =>
            item.TrialDirectory == Path.GetFileName(directory) && !item.Deleted);
        string rejectionId = previous?.Id ?? Guid.NewGuid().ToString("N");
        string auditDirectory = AuditDirectory(run);
        Directory.CreateDirectory(auditDirectory);
        string auditPath = Path.Combine(auditDirectory, $"quota-{rejectionId}.json");
        string agentDirectory = Path.Combine(directory, "agent");
        AgentEvidenceSummary[] evidence = (Directory.Exists(agentDirectory)
                ? Directory.EnumerateFiles(agentDirectory, "result.json", SearchOption.AllDirectories)
                : [])
            .Select(path => JsonSerializer.Deserialize<AgentEvidenceSummary>(
                File.ReadAllText(path), StateJson.Options)
                ?? throw new RunException($"null agent evidence: {path}"))
            .ToArray();
        decimal? credits = evidence.Any(item => item.Cost is not null)
            ? evidence.Sum(item => item.Cost ?? 0)
            : null;
        var rejection = new QuotaRejection(
            rejectionId, Path.GetFileName(directory), attempt.Task.TaskId,
            attempt.AttemptNumber, DateTimeOffset.UtcNow, auditPath, false,
            credits, evidence.Length == 0 ? null : evidence.Sum(item => item.DurationMilliseconds),
            KnownUsd(evidence));
        AtomicFile.Replace(auditPath, JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            rejectionId,
            runDirectory = run,
            trialDirectory = directory,
            taskId = attempt.Task.TaskId,
            attempt = attempt.AttemptNumber,
            configuration = configuration with { Credentials = new SecretSettings() },
            originalRuntimeCommit = (string?)null,
            runtimeVersion = typeof(RunBatch).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            diagnosis = failure?.Reason ?? "prepared work did not start after run quota pause",
            reset = failure?.Reset,
            knownCredits = credits,
            knownCostUsd = rejection.KnownCostUsd,
            knownAgentMilliseconds = rejection.KnownAgentMilliseconds,
            usage = evidence.Select(item => item.Usage),
            payloads = Payloads(directory).Select(Path.GetFileName).ToArray(),
            rejectedAt = rejection.Cutoff
        }, StateJson.Options));
        state = state with
        {
            Rejections = previous is null ? state.Rejections.Append(rejection).ToArray()
                : state.Rejections.Select(item => item.Id == rejectionId ? rejection : item).ToArray()
        };
        Save();
        if (delete)
            DeletePayload(rejection);
    }

    public string Finish(int pending, bool cancelled = false, bool failed = false)
    {
        lock (sync)
        {
            string status = failed ? "failed" : cancelled ? "cancelled" :
                quota is not null ? "quota-paused" : pending == 0 ? "completed" : "batch-limit";
            Update(Current() with
            {
                Status = status,
                Finished = DateTimeOffset.UtcNow,
                Pending = pending,
                Quota = quota
            });
            return status;
        }
    }

    private static string AuditDirectory(string directory)
    {
        for (DirectoryInfo? parent = new(directory); parent is not null; parent = parent.Parent)
            if (Directory.Exists(Path.Combine(parent.FullName, ".git")) ||
                File.Exists(Path.Combine(parent.FullName, ".git")))
                return Path.Combine(parent.FullName, ".github", "tracking");
        return Path.Combine(directory, ".github", "tracking");
    }

    private string ValidateTrialPath(string directory)
    {
        string full = Path.GetFullPath(directory);
        if (Path.GetDirectoryName(full) != run ||
            !Path.GetFileName(full).Contains("__", StringComparison.Ordinal) ||
            (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            throw new RunException($"unsafe quota payload path: {directory}");
        return full;
    }

    private static IEnumerable<string> Payloads(string directory) =>
        Directory.EnumerateFileSystemEntries(directory).Where(path =>
            Path.GetFileName(path) is not ("trial-retries" or "verifier-retries" or "resume-history"));

    private void RecoverDeletions()
    {
        foreach (QuotaRejection rejection in state.Rejections.Where(item => !item.Deleted).ToArray())
            DeletePayload(rejection);
    }

    private void DeletePayload(QuotaRejection rejection)
    {
        string expectedAudit = Path.Combine(AuditDirectory(run), $"quota-{rejection.Id}.json");
        if (Path.GetFullPath(rejection.AuditPath) != expectedAudit || !File.Exists(expectedAudit))
            throw new RunException($"quota deletion audit is missing or mismatched: {rejection.AuditPath}");
        string directory = Path.Combine(run, rejection.TrialDirectory);
        RetainedRunMetadata? retained = RetainedRunMetadataStore.Read(run);
        if (retained is not null && (rejection.Attempt > retained.Configuration.K ||
            !retained.Tasks.Any(task => task.TaskId == rejection.TaskId &&
                RetainedRunResumePlanner.TrialDirectoryName(
                    task.TaskId, task.SourceChecksum, rejection.Attempt, retained.Configuration.K) ==
                rejection.TrialDirectory)))
            throw new RunException($"quota rejection does not match retained trial identity: {directory}");
        JsonObject audit;
        try
        {
            audit = JsonNode.Parse(File.ReadAllText(expectedAudit)) as JsonObject
                ?? throw new RunException($"invalid quota deletion audit: {expectedAudit}");
            if (audit["schemaVersion"]?.GetValue<string>() != "1.0" ||
                audit["rejectionId"]?.GetValue<string>() != rejection.Id ||
                audit["runDirectory"]?.GetValue<string>() != run ||
                audit["trialDirectory"]?.GetValue<string>() != directory ||
                audit["taskId"]?.GetValue<string>() != rejection.TaskId ||
                audit["attempt"]?.GetValue<int>() != rejection.Attempt)
                throw new RunException($"quota deletion audit identity mismatch: {expectedAudit}");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            throw new RunException($"invalid quota deletion audit: {expectedAudit}: {exception.Message}");
        }
        if (Directory.Exists(directory))
        {
            directory = ValidateTrialPath(directory);
            rejection = rejection with { Cutoff = DateTimeOffset.UtcNow };
            audit["rejectedAt"] = JsonSerializer.SerializeToNode(rejection.Cutoff);
            string agent = Path.Combine(directory, "agent");
            if (Directory.Exists(agent))
            {
                AgentEvidenceSummary[] summaries = Directory.EnumerateFiles(
                        agent, "result.json", SearchOption.AllDirectories)
                    .Select(path => JsonSerializer.Deserialize<AgentEvidenceSummary>(
                        File.ReadAllText(path), StateJson.Options)
                        ?? throw new RunException($"null agent evidence: {path}"))
                    .ToArray();
                if (summaries.Length > 0)
                {
                    decimal? credits = summaries.Any(item => item.Cost is not null)
                        ? summaries.Sum(item => item.Cost ?? 0) : null;
                    rejection = rejection with
                    {
                        KnownCredits = credits,
                        KnownAgentMilliseconds = summaries.Sum(item => item.DurationMilliseconds),
                        KnownCostUsd = KnownUsd(summaries)
                    };
                    audit["knownCredits"] = JsonSerializer.SerializeToNode(credits);
                    audit["knownCostUsd"] = JsonSerializer.SerializeToNode(rejection.KnownCostUsd);
                    audit["knownAgentMilliseconds"] = rejection.KnownAgentMilliseconds;
                    audit["usage"] = JsonSerializer.SerializeToNode(
                        summaries.Select(item => item.Usage), StateJson.Options);
                }
            }
            AtomicFile.Replace(expectedAudit, audit.ToJsonString(StateJson.Options));
            foreach (string entry in Payloads(directory).ToArray())
            {
                if (Directory.Exists(entry))
                    Directory.Delete(entry, recursive: true);
                else
                    File.Delete(entry);
            }
        }
        state = state with
        {
            Rejections = state.Rejections.Select(item =>
                item.Id == rejection.Id ? rejection with { Deleted = true } : item).ToArray()
        };
        Save();
    }

    private BatchInvocation Current() =>
        current ?? throw new RunException("batch has not started");

    private static decimal? KnownUsd(IEnumerable<AgentEvidenceSummary> summaries)
    {
        decimal?[] costs = summaries.Select(item => item.Usage?.CostUsd ?? item.Cost * 0.01m).ToArray();
        return costs.Any(cost => cost.HasValue) ? costs.Sum() : null;
    }

    private void Update(BatchInvocation batch, bool save = true)
    {
        current = batch;
        state = state with
        {
            Invocations = state.Invocations.Select(item => item.Id == batch.Id ? batch : item).ToArray()
        };
        if (save)
            Save();
    }

    private void Save() => AtomicFile.Replace(
        Path.Combine(run, FileName), JsonSerializer.Serialize(state, StateJson.Options));

    public void Dispose() => writer.Dispose();
}
