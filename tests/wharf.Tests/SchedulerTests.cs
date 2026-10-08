using System.Collections.Concurrent;
using Wharf.Scheduler;

namespace Wharf.Tests;

public sealed class SchedulerTests
{
    [Fact]
    public async Task AdmitsAttemptsInDeterministicOrderAndRespectsConcurrency()
    {
        var runner = new RecordingRunner();
        var sink = new RecordingSink();
        var scheduler = new DeterministicScheduler(runner, sink);
        SchedulerResult result = await scheduler.RunAsync(
            [
                new ScheduledAttempt("b", "logical-b", "task-b"),
                new ScheduledAttempt("a", "logical-a", "task-a"),
                new ScheduledAttempt("c", "logical-c", "task-c")
            ],
            new SchedulerOptions(2, 3));

        Assert.Equal(["a", "b", "c"], result.StartedAttempts.Select(attempt => attempt.AttemptId));
        Assert.Equal(2, runner.MaximumActive);
        Assert.Equal(3, result.Counts.Completed);
    }

    [Fact]
    public async Task BanksSuccessfulLogicalAttemptAndSkipsDuplicate()
    {
        var runner = new RecordingRunner();
        var scheduler = new DeterministicScheduler(runner, new RecordingSink());
        SchedulerResult result = await scheduler.RunAsync(
            [
                new ScheduledAttempt("a-1", "logical-a", "task-a"),
                new ScheduledAttempt("a-2", "logical-a", "task-a"),
                new ScheduledAttempt("b-1", "logical-b", "task-b")
            ],
            new SchedulerOptions(1, 3));

        Assert.Equal(["a-1", "b-1"], result.StartedAttempts.Select(attempt => attempt.AttemptId));
        Assert.Contains("logical-a", result.BankedLogicalAttempts);
    }

    [Fact]
    public async Task AdmitsEveryKWaveLongestFirstBeforeNextWave()
    {
        var scheduler = new DeterministicScheduler(
            new RecordingRunner(),
            new RecordingSink());

        SchedulerResult result = await scheduler.RunAsync(
            [
                new ScheduledAttempt("short-2", "short", "short", 2, 10),
                new ScheduledAttempt("long-2", "long", "long", 2, 100),
                new ScheduledAttempt("short-1", "short", "short", 1, 10),
                new ScheduledAttempt("long-1", "long", "long", 1, 100)
            ],
            new SchedulerOptions(1, 4));

        Assert.Equal(
            ["long-1", "short-1"],
            result.StartedAttempts.Select(attempt => attempt.AttemptId));
    }

    [Fact]
    public async Task EnforcesMaximumTrialLimit()
    {
        var scheduler = new DeterministicScheduler(
            new RecordingRunner(),
            new RecordingSink());

        SchedulerResult result = await scheduler.RunAsync(
            Enumerable.Range(1, 5)
                .Select(number => new ScheduledAttempt($"attempt-{number}", $"logical-{number}", $"task-{number}"))
                .ToArray(),
            new SchedulerOptions(4, 2));

        Assert.Equal(2, result.StartedAttempts.Count);
        Assert.Equal(3, result.Counts.Pending);
    }

    [Fact]
    public async Task RecordsFailureAndStopsBankingThatAttempt()
    {
        var runner = new RecordingRunner(failAttempt: "a");
        var sink = new RecordingSink();
        var scheduler = new DeterministicScheduler(runner, sink);

        SchedulerResult result = await scheduler.RunAsync(
            [
                new ScheduledAttempt("a", "logical-a", "task-a"),
                new ScheduledAttempt("b", "logical-b", "task-b")
            ],
            new SchedulerOptions(1, 2));

        Assert.Equal(1, result.Counts.Errored);
        Assert.DoesNotContain("logical-a", result.BankedLogicalAttempts);
        Assert.Contains(sink.Events, item =>
            item.AttemptId == "a" && item.Lifecycle == SchedulerLifecycle.Errored);
    }

    [Fact]
    public async Task CancellationMarksPendingAttemptsCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var sink = new RecordingSink();
        var scheduler = new DeterministicScheduler(
            new RecordingRunner(),
            sink);

        SchedulerResult result = await scheduler.RunAsync(
            [
                new ScheduledAttempt("a", "logical-a", "task-a"),
                new ScheduledAttempt("b", "logical-b", "task-b")
            ],
            new SchedulerOptions(1, 2),
            cancellation.Token);

        Assert.Equal(2, result.Counts.Cancelled);
        Assert.All(sink.Events, item => Assert.Equal(SchedulerLifecycle.Cancelled, item.Lifecycle));
    }

    private sealed class RecordingRunner(string? failAttempt = null) : ITrialRunner
    {
        private int active;
        private int maximumActive;

        public int MaximumActive => maximumActive;

        public async Task<TrialExecutionResult> RunAsync(
            ScheduledAttempt attempt,
            Func<string, Task> lifecycle,
            CancellationToken cancellationToken)
        {
            int current = Interlocked.Increment(ref active);
            int observed;
            do
            {
                observed = maximumActive;
            } while (current > observed &&
                     Interlocked.CompareExchange(ref maximumActive, current, observed) != observed);

            await lifecycle(SchedulerLifecycle.Agent);
            await Task.Delay(1, cancellationToken);
            Interlocked.Decrement(ref active);
            return attempt.AttemptId == failAttempt
                ? new TrialExecutionResult(false, "failed")
                : new TrialExecutionResult(true);
        }
    }

    private sealed class RecordingSink : ISchedulerEventSink
    {
        public ConcurrentQueue<SchedulerEvent> Events { get; } = new();

        public Task RecordAsync(SchedulerEvent schedulerEvent, CancellationToken cancellationToken)
        {
            Events.Enqueue(schedulerEvent);
            return Task.CompletedTask;
        }
    }
}
