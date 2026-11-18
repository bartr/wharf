using Wharf.Recovery;
using Wharf.Tasks;

namespace Wharf.Run;

internal static class BatchRunPlanning
{
    public static RetainedResumePlan Plan(string directory, int size)
    {
        RunBatch.ValidateSize(size);
        string run = Path.GetFullPath(directory);
        if (RetainedRunMetadataStore.Read(run) is null)
            throw new RunException($"batch continuation requires a retained run-plan.json: {run}");
        RunBatch.Read(run);
        RetainedResumePlan plan = new RetainedRunResumePlanner().Plan(run);
        if (plan.Configuration.NoOp || plan.Configuration.Oracle)
            throw new RunException("batch mode requires an AI agent run");
        return plan;
    }

    public static void Preview(
        IReadOnlyList<TaskAttempt> attempts, int size, int planned, string? version = null)
    {
        RunBatch.ValidateSize(size);
        TaskAttempt[] selected = attempts.Take(size).ToArray();
        Console.WriteLine(
            $"batch requested={size} selected={selected.Length} pending={attempts.Count} " +
            $"remaining={attempts.Count - selected.Length} planned={planned} cli={version ?? "unresolved"}");
        foreach (TaskAttempt attempt in selected)
            Console.WriteLine($"selected task={attempt.Task.TaskId} attempt={attempt.AttemptNumber} id={attempt.AttemptId}");
    }
}
