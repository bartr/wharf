namespace Wharf.Scheduler;

public static class SchedulerLifecycle
{
    public const string Pending = "pending";
    public const string Preparing = "preparing";
    public const string Starting = "starting";
    public const string Agent = "agent";
    public const string Verifier = "verifier";
    public const string Completed = "completed";
    public const string Errored = "errored";
    public const string Cancelled = "cancelled";
}

public sealed record ScheduledAttempt(
    string AttemptId,
    string LogicalAttemptId,
    string TaskId);

public sealed record TrialExecutionResult(
    bool Succeeded,
    string? Error = null);

public sealed record SchedulerEvent(
    string AttemptId,
    string LogicalAttemptId,
    string Lifecycle,
    string? Detail = null);

public sealed record SchedulerOptions(
    int MaximumConcurrentTrials,
    int MaximumTrials)
{
    public void Validate()
    {
        if (MaximumConcurrentTrials < 1 || MaximumTrials < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumConcurrentTrials),
                "scheduler limits must be positive");
        }
    }
}

public sealed record SchedulerCounts(
    int Pending,
    int Preparing,
    int Starting,
    int Agent,
    int Verifier,
    int Completed,
    int Errored,
    int Cancelled);

public sealed record SchedulerResult(
    IReadOnlyList<ScheduledAttempt> StartedAttempts,
    IReadOnlySet<string> BankedLogicalAttempts,
    SchedulerCounts Counts);

public interface ITrialRunner
{
    Task<TrialExecutionResult> RunAsync(
        ScheduledAttempt attempt,
        Func<string, Task> lifecycle,
        CancellationToken cancellationToken);
}

public interface ISchedulerEventSink
{
    Task RecordAsync(SchedulerEvent schedulerEvent, CancellationToken cancellationToken);
}

public sealed class SchedulerException(string message) : Exception(message);
