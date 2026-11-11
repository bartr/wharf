using Wharf.Recovery;
using Wharf.State;

namespace Wharf.Tests;

public sealed class RecoveryPlannerTests
{
    [Fact]
    public async Task ResumesOnlyUnfinishedAndRetryableAttemptsInStableOrder()
    {
        using TemporaryDirectory temporary = new();
        var runId = new RunId("run-recovery");
        string eventsPath = Path.Combine(temporary.Path, "events.jsonl");
        File.WriteAllLines(
            eventsPath,
            [
                Json(Event(runId, "e1", 1, StateEventTypes.LogicalAttemptStarted, StateStatuses.Running, "logical-complete")),
                Json(Event(runId, "e2", 2, StateEventTypes.TrialCompleted, StateStatuses.Completed, "logical-complete")),
                Json(Event(runId, "e3", 3, StateEventTypes.LogicalAttemptStarted, StateStatuses.Running, "logical-retry")),
                Json(Event(runId, "e4", 4, StateEventTypes.TrialFailed, StateStatuses.Failed, "logical-retry",
                    new Dictionary<string, string> { ["retryable"] = "true" })),
                Json(Event(runId, "e5", 5, StateEventTypes.LogicalAttemptStarted, StateStatuses.Running, "logical-running")),
                Json(Event(runId, "e6", 6, StateEventTypes.InfrastructureRetryStarted, StateStatuses.Running, "logical-retry",
                    infrastructureRetryId: "retry-1"))
            ]);

        var planner = new RunRecoveryPlanner(
            new FakeResources(new HashSet<string>(
                ["trial-orphan", "trial-running"],
                StringComparer.Ordinal)));
        RecoveryPlan plan = await planner.PlanAsync(
            runId,
            eventsPath,
            [
                new RecoveryAttempt("z", "logical-running", "trial-running"),
                new RecoveryAttempt("a", "logical-complete", "trial-complete"),
                new RecoveryAttempt("b", "logical-retry", "trial-retry")
            ]);

        Assert.Equal(["b", "z"], plan.AttemptsToResume.Select(attempt => attempt.AttemptId));
        Assert.Contains("logical-complete", plan.CompletedLogicalAttempts);
        Assert.Equal(1, plan.InfrastructureRetryCounts["logical-retry"]);
        Assert.Contains("trial-orphan", plan.OrphanedResourceIds);
    }

    [Fact]
    public async Task DoesNotResumeDeterministicFailure()
    {
        using TemporaryDirectory temporary = new();
        var runId = new RunId("run-failed");
        string path = Path.Combine(temporary.Path, "events.jsonl");
        File.WriteAllText(
            path,
            Json(Event(
                runId,
                "e1",
                1,
                StateEventTypes.TrialFailed,
                StateStatuses.Failed,
                "logical-failed",
                new Dictionary<string, string> { ["retryable"] = "false" })));

        RecoveryPlan plan = await new RunRecoveryPlanner().PlanAsync(
            runId,
            path,
            [new RecoveryAttempt("a", "logical-failed", "trial")]);

        Assert.Empty(plan.AttemptsToResume);
    }

    private static StateEvent Event(
        RunId runId,
        string eventId,
        long sequence,
        string type,
        string status,
        string logicalAttempt,
        IReadOnlyDictionary<string, string>? payload = null,
        string? infrastructureRetryId = null) =>
        new(
            StateSchema.Version,
            new EventId(eventId),
            sequence,
            runId,
            type,
            status,
            LogicalAttemptId: new LogicalAttemptId(logicalAttempt),
            InfrastructureRetryId: infrastructureRetryId is null ? null : new InfrastructureRetryId(infrastructureRetryId),
            Payload: payload);

    private static string Json(StateEvent stateEvent) =>
        System.Text.Json.JsonSerializer.Serialize(stateEvent, StateJson.Options);

    private sealed class FakeResources(IReadOnlySet<string> resources) : ILiveResourceReconciler
    {
        public Task<LiveResourceSnapshot> InspectAsync(
            RunId runId,
            CancellationToken cancellationToken) =>
            Task.FromResult(new LiveResourceSnapshot(resources));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"wharf-recovery-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
