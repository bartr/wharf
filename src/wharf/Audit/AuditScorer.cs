namespace Wharf.Audit;

public sealed class AuditScorer
{
    public AuditReport Score(AuditRun run)
    {
        Validate(run);
        string[] expected = run.ExpectedTaskIds.Distinct(StringComparer.Ordinal).ToArray();
        Dictionary<string, AuditTask> observed = run.ObservedTasks
            .GroupBy(task => task.TaskId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

        int passingAttempts = 0;
        int passingTasks = 0;
        double coverageTotal = 0;
        bool regression = false;
        double enterpriseCoverageTotal = 0;
        double enterprisePassTotal = 0;
        double? cost = null;
        long? inputTokens = null;
        long? outputTokens = null;
        long? cacheTokens = null;
        long? requests = null;
        long? steps = null;
        TimeSpan? agentTime = null;
        TimeSpan? trialTime = null;
        bool costIncomplete = false;
        int missingMeasurements = 0, invalidMeasurements = 0, partialMeasurements = 0, zeroMeasurements = 0;
        int observedAttempts = 0, expectedAttempts = expected.Length;

        foreach (string taskId in expected)
        {
            if (!observed.TryGetValue(taskId, out AuditTask? task) || task.Attempts.Count == 0)
            {
                continue;
            }

            if (task.Attempts.Any(IsPassing))
            {
                passingTasks++;
            }

            double taskCoverageScore = 0;
            double taskPassScore = 0;
            foreach (AuditAttempt attempt in task.Attempts)
            {
                observedAttempts++;
                if (IsPassing(attempt))
                {
                    passingAttempts++;
                }
                (double coverageScore, double passScore) = AttemptScore(attempt);
                taskCoverageScore += coverageScore;
                taskPassScore += passScore;
                coverageTotal += Numeric(attempt.NewTestCoverage, ref missingMeasurements, ref invalidMeasurements, ref partialMeasurements, ref zeroMeasurements);
                regression |= attempt.ExistingTestRegression.State is MeasurementState.Valid or MeasurementState.Zero &&
                    attempt.ExistingTestRegression.Value == true;
                cost = Add(cost, attempt.Cost, ref missingMeasurements, ref invalidMeasurements, ref partialMeasurements, ref zeroMeasurements);
                costIncomplete |= attempt.Cost.State is MeasurementState.Missing or MeasurementState.Invalid;
                inputTokens = Add(inputTokens, attempt.InputTokens, ref missingMeasurements, ref invalidMeasurements, ref partialMeasurements, ref zeroMeasurements);
                outputTokens = Add(outputTokens, attempt.OutputTokens, ref missingMeasurements, ref invalidMeasurements, ref partialMeasurements, ref zeroMeasurements);
                cacheTokens = Add(cacheTokens, attempt.CacheTokens, ref missingMeasurements, ref invalidMeasurements, ref partialMeasurements, ref zeroMeasurements);
                requests = Add(requests, attempt.Requests, ref missingMeasurements, ref invalidMeasurements, ref partialMeasurements, ref zeroMeasurements);
                steps = Add(steps, attempt.Steps, ref missingMeasurements, ref invalidMeasurements, ref partialMeasurements, ref zeroMeasurements);
                agentTime = Add(agentTime, attempt.AgentTime, ref missingMeasurements, ref invalidMeasurements, ref partialMeasurements, ref zeroMeasurements);
                trialTime = Add(trialTime, attempt.TrialTime, ref missingMeasurements, ref invalidMeasurements, ref partialMeasurements, ref zeroMeasurements);
            }

            enterpriseCoverageTotal += taskCoverageScore / task.Attempts.Count;
            enterprisePassTotal += taskPassScore / task.Attempts.Count;
        }

        int missingTasks = expected.Count(taskId => !observed.ContainsKey(taskId));
        double enterpriseCoverageComponent = expected.Length == 0
            ? 0
            : enterpriseCoverageTotal / expected.Length;
        double enterprisePassComponent = expected.Length == 0
            ? 0
            : enterprisePassTotal / expected.Length;
        return new AuditReport(
            run.RunId,
            observedAttempts == 0 ? 0 : (double)passingAttempts / observedAttempts,
            passingAttempts,
            observedAttempts,
            observed.Count == 0 ? 0 : (double)passingTasks / observed.Count,
            passingTasks,
            observed.Count,
            enterpriseCoverageComponent + enterprisePassComponent,
            enterpriseCoverageComponent,
            enterprisePassComponent,
            expected.Length,
            observedAttempts == 0 ? 0 : coverageTotal / observedAttempts,
            regression,
            costIncomplete ? null : cost, inputTokens, outputTokens, cacheTokens, requests, steps, agentTime, trialTime,
            new AuditCompleteness(expected.Length, observed.Count, missingTasks, expectedAttempts, observedAttempts,
                missingMeasurements, invalidMeasurements, partialMeasurements, zeroMeasurements));
    }

    private static (double Coverage, double Pass) AttemptScore(AuditAttempt attempt)
    {
        if (attempt.ExistingTestRegression.State is MeasurementState.Valid or MeasurementState.Zero &&
            attempt.ExistingTestRegression.Value == true)
        {
            return (0, 0);
        }

        double coverage = attempt.NewTestCoverage.State is MeasurementState.Valid or MeasurementState.Partial or MeasurementState.Zero
            ? Math.Clamp(attempt.NewTestCoverage.Value.GetValueOrDefault(), 0, 1)
            : 0;
        return (coverage * 80, IsPassing(attempt) ? 20 : 0);
    }

    private static bool IsPassing(AuditAttempt attempt) =>
        attempt.Passed.State is MeasurementState.Valid or MeasurementState.Zero &&
        attempt.Passed.Value == true;

    private static double Numeric(Measurement<double> measurement, ref int missing, ref int invalid, ref int partial, ref int zero)
    {
        CountState(measurement.State, ref missing, ref invalid, ref partial, ref zero);
        return measurement.State is MeasurementState.Valid or MeasurementState.Partial ? measurement.Value ?? 0 : 0;
    }

    private static T? Add<T>(T? total, Measurement<T> measurement, ref int missing, ref int invalid, ref int partial, ref int zero)
        where T : struct
    {
        CountState(measurement.State, ref missing, ref invalid, ref partial, ref zero);
        if (measurement.State is not (MeasurementState.Valid or MeasurementState.Partial) || !measurement.Value.HasValue)
        {
            return total;
        }

        if (!total.HasValue) return measurement.Value;
        if (typeof(T) == typeof(TimeSpan))
        {
            TimeSpan sum = (TimeSpan)(object)total.Value + (TimeSpan)(object)measurement.Value.Value;
            return (T)(object)sum;
        }
        return (dynamic)total.Value + (dynamic)measurement.Value.Value;
    }

    private static void CountState(MeasurementState state, ref int missing, ref int invalid, ref int partial, ref int zero)
    {
        switch (state)
        {
            case MeasurementState.Missing: missing++; break;
            case MeasurementState.Invalid: invalid++; break;
            case MeasurementState.Partial: partial++; break;
            case MeasurementState.Zero: zero++; break;
        }
    }

    private static void Validate(AuditRun run)
    {
        if (string.IsNullOrWhiteSpace(run.RunId) || run.ExpectedTaskIds.Any(string.IsNullOrWhiteSpace))
            throw new AuditContractException("run and expected task IDs are required");
        if (run.ExpectedTaskIds.Count != run.ExpectedTaskIds.Distinct(StringComparer.Ordinal).Count())
            throw new AuditContractException("expected task IDs must be unique");
        if (run.ObservedTasks.Select(task => task.TaskId).Distinct(StringComparer.Ordinal).Count() !=
            run.ObservedTasks.Count)
            throw new AuditContractException("observed task IDs must be unique");
        var expected = run.ExpectedTaskIds.ToHashSet(StringComparer.Ordinal);
        foreach (AuditTask task in run.ObservedTasks)
        {
            if (string.IsNullOrWhiteSpace(task.TaskId) || task.Attempts.Count == 0 ||
                !expected.Contains(task.TaskId) ||
                task.Attempts.Any(attempt =>
                    string.IsNullOrWhiteSpace(attempt.AttemptId) ||
                    attempt.AttemptNumber <= 0) ||
                task.Attempts.Select(attempt => attempt.AttemptId)
                    .Distinct(StringComparer.Ordinal).Count() != task.Attempts.Count ||
                task.Attempts.Select(attempt => attempt.AttemptNumber)
                    .Distinct().Count() != task.Attempts.Count)
                throw new AuditContractException("observed tasks require unique attempts");
        }
    }
}
