namespace Wharf.Tasks;

public static class TaskSelector
{
    public static IReadOnlyList<HarborTask> Select(
        IReadOnlyList<HarborTask> tasks,
        TaskSelection selection)
    {
        if (selection.Attempts < 1)
        {
            throw new HarborTaskException("attempts must be at least 1");
        }

        IEnumerable<HarborTask> filtered = tasks
            .OrderBy(task => task.TaskId, StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(selection.Language))
        {
            filtered = filtered.Where(task =>
                task.Language.Equals(selection.Language, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(selection.Include))
        {
            filtered = filtered.Where(task => Matches(task, selection.Include));
        }

        if (!string.IsNullOrWhiteSpace(selection.Exclude))
        {
            filtered = filtered.Where(task => !Matches(task, selection.Exclude));
        }

        List<HarborTask> ordered = filtered.ToList();
        if (!string.IsNullOrWhiteSpace(selection.Shard))
        {
            (int index, int count) = ParseShard(selection.Shard);
            ordered = ordered
                .Where((_, position) => position % count == index)
                .ToList();
        }

        if (selection.Sample is int sample)
        {
            if (sample < 0 || sample > ordered.Count)
            {
                throw new HarborTaskException($"sample must be between 0 and {ordered.Count}");
            }

            ordered = Shuffle(ordered, selection.Seed).Take(sample).ToList();
            ordered.Sort((left, right) => StringComparer.Ordinal.Compare(left.TaskId, right.TaskId));
        }

        return ordered;
    }

    public static IReadOnlyList<TaskAttempt> ExpandAttempts(
        IReadOnlyList<HarborTask> tasks,
        int attempts)
    {
        if (attempts < 1)
        {
            throw new HarborTaskException("attempts must be at least 1");
        }

        return tasks.SelectMany(task => Enumerable.Range(1, attempts)
                .Select(number => new TaskAttempt($"{task.TaskId}#attempt-{number}", task, number)))
            .ToArray();
    }

    public static IReadOnlyList<TaskAttempt> OrderAttemptsForAdmission(
        IReadOnlyList<TaskAttempt> attempts,
        bool shortestFirst = false)
    {
        IOrderedEnumerable<TaskAttempt> ordered = attempts
            .OrderBy(attempt => attempt.AttemptNumber);
        Func<TaskAttempt, double> duration = attempt =>
            attempt.Task.SchedulingDurationSeconds ??
            (attempt.Task.EstimatedAgentDurationSeconds ?? 0) +
            (attempt.Task.EstimatedVerifierDurationSeconds ?? 0);
        ordered = shortestFirst
            ? ordered.ThenBy(duration)
            : ordered.ThenByDescending(duration);
        return ordered
            .ThenBy(attempt => attempt.Task.TaskId, StringComparer.Ordinal)
            .ThenBy(attempt => attempt.AttemptId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool Matches(HarborTask task, string expression) =>
        task.TaskId.Equals(expression, StringComparison.OrdinalIgnoreCase) ||
        task.Category.Equals(expression, StringComparison.OrdinalIgnoreCase) ||
        task.Language.Equals(expression, StringComparison.OrdinalIgnoreCase);

    private static (int Index, int Count) ParseShard(string value)
    {
        string[] parts = value.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], out int index) ||
            !int.TryParse(parts[1], out int count) ||
            count < 1 ||
            index < 0 ||
            index >= count)
        {
            throw new HarborTaskException($"invalid shard '{value}', expected INDEX/COUNT");
        }

        return (index, count);
    }

    private static IEnumerable<HarborTask> Shuffle(IReadOnlyList<HarborTask> tasks, int seed)
    {
        var random = new Random(seed);
        var copy = tasks.ToList();
        for (int index = copy.Count - 1; index > 0; index--)
        {
            int swap = random.Next(index + 1);
            (copy[index], copy[swap]) = (copy[swap], copy[index]);
        }

        return copy;
    }
}
