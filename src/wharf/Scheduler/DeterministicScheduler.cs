namespace Wharf.Scheduler;

public sealed class DeterministicScheduler
{
    private readonly ITrialRunner runner;
    private readonly ISchedulerEventSink sink;

    public DeterministicScheduler(ITrialRunner runner, ISchedulerEventSink sink)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    public async Task<SchedulerResult> RunAsync(
        IReadOnlyList<ScheduledAttempt> attempts,
        SchedulerOptions options,
        CancellationToken cancellationToken = default)
    {
        options.Validate();
        IReadOnlyList<ScheduledAttempt> ordered = attempts
            .OrderBy(attempt => attempt.AttemptNumber)
            .ThenByDescending(attempt =>
                (attempt.EstimatedAgentDurationSeconds ?? 0) +
                (attempt.EstimatedVerifierDurationSeconds ?? 0))
            .ThenBy(attempt => attempt.TaskId, StringComparer.Ordinal)
            .ThenBy(attempt => attempt.AttemptId, StringComparer.Ordinal)
            .ToArray();
        var pending = new Queue<ScheduledAttempt>(ordered);
        var started = new List<ScheduledAttempt>();
        var banked = new HashSet<string>(StringComparer.Ordinal);
        var states = ordered.ToDictionary(
            attempt => attempt.AttemptId,
            _ => SchedulerLifecycle.Pending,
            StringComparer.Ordinal);
        var active = new List<RunningAttempt>();
        int admissionLimit = Math.Min(options.MaximumConcurrentTrials, options.MaximumTrials);

        while (pending.Count > 0 || active.Count > 0)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                while (pending.Count > 0)
                {
                    ScheduledAttempt cancelled = pending.Dequeue();
                    states[cancelled.AttemptId] = SchedulerLifecycle.Cancelled;
                    await RecordAsync(
                        cancelled,
                        SchedulerLifecycle.Cancelled,
                        "cancelled",
                        CancellationToken.None);
                }
            }

            while (active.Count < admissionLimit &&
                   !cancellationToken.IsCancellationRequested &&
                   started.Count < options.MaximumTrials &&
                   pending.Count > 0)
            {
                ScheduledAttempt attempt = pending.Dequeue();
                if (banked.Contains(attempt.LogicalAttemptId))
                {
                    states[attempt.AttemptId] = SchedulerLifecycle.Completed;
                    await RecordAsync(attempt, SchedulerLifecycle.Completed, "banked", cancellationToken);
                    continue;
                }

                started.Add(attempt);
                states[attempt.AttemptId] = SchedulerLifecycle.Starting;
                await RecordAsync(attempt, SchedulerLifecycle.Starting, null, cancellationToken);
                Task<TrialExecutionResult> task = RunAttemptAsync(attempt, cancellationToken);
                active.Add(new RunningAttempt(attempt, task));
            }

            if (active.Count == 0)
            {
                break;
            }

            Task<TrialExecutionResult> completedTask = await Task.WhenAny(
                active.Select(item => item.Task));
            RunningAttempt completed = active.First(item => item.Task == completedTask);
            active.Remove(completed);
            TrialExecutionResult result;
            try
            {
                result = await completed.Task;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                result = new TrialExecutionResult(false, "cancelled");
            }
            string lifecycle = result.Succeeded
                ? SchedulerLifecycle.Completed
                : cancellationToken.IsCancellationRequested
                    ? SchedulerLifecycle.Cancelled
                    : SchedulerLifecycle.Errored;
            states[completed.Attempt.AttemptId] = lifecycle;
            if (result.Succeeded)
            {
                banked.Add(completed.Attempt.LogicalAttemptId);
            }

            await RecordAsync(
                completed.Attempt,
                lifecycle,
                result.Error,
                lifecycle == SchedulerLifecycle.Cancelled
                    ? CancellationToken.None
                    : cancellationToken);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            foreach (ScheduledAttempt attempt in pending)
            {
                states[attempt.AttemptId] = SchedulerLifecycle.Cancelled;
                await RecordAsync(attempt, SchedulerLifecycle.Cancelled, "cancelled", CancellationToken.None);
            }
        }

        return new SchedulerResult(
            started,
            banked,
            new SchedulerCounts(
                states.Values.Count(state => state == SchedulerLifecycle.Pending),
                states.Values.Count(state => state == SchedulerLifecycle.Preparing),
                states.Values.Count(state => state == SchedulerLifecycle.Starting),
                states.Values.Count(state => state == SchedulerLifecycle.Agent),
                states.Values.Count(state => state == SchedulerLifecycle.Verifier),
                states.Values.Count(state => state == SchedulerLifecycle.Completed),
                states.Values.Count(state => state == SchedulerLifecycle.Errored),
                states.Values.Count(state => state == SchedulerLifecycle.Cancelled)));
    }

    private async Task<TrialExecutionResult> RunAttemptAsync(
        ScheduledAttempt attempt,
        CancellationToken cancellationToken)
    {
        await RecordAsync(attempt, SchedulerLifecycle.Preparing, null, cancellationToken);
        return await runner.RunAsync(
            attempt,
            lifecycle => RecordAsync(attempt, lifecycle, null, cancellationToken),
            cancellationToken);
    }

    private Task RecordAsync(
        ScheduledAttempt attempt,
        string lifecycle,
        string? detail,
        CancellationToken cancellationToken) =>
        sink.RecordAsync(
            new SchedulerEvent(attempt.AttemptId, attempt.LogicalAttemptId, lifecycle, detail),
            cancellationToken);

    private sealed record RunningAttempt(
        ScheduledAttempt Attempt,
        Task<TrialExecutionResult> Task);
}
