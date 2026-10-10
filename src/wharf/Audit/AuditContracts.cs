namespace Wharf.Audit;

public enum MeasurementState
{
    Missing,
    Invalid,
    Partial,
    Zero,
    Valid
}

public readonly record struct Measurement<T>(MeasurementState State, T? Value)
    where T : struct
{
    public static Measurement<T> MissingValue() => new(MeasurementState.Missing, default);
    public static Measurement<T> InvalidValue() => new(MeasurementState.Invalid, default);
    public static Measurement<T> PartialValue(T value) => new(MeasurementState.Partial, value);
    public static Measurement<T> ZeroValue() => new(MeasurementState.Zero, default);
    public static Measurement<T> ValidValue(T value) => new(MeasurementState.Valid, value);
}

public sealed record AuditAttempt(
    string AttemptId,
    int AttemptNumber,
    Measurement<bool> Passed,
    Measurement<double> NewTestCoverage,
    Measurement<bool> ExistingTestRegression,
    Measurement<double> Cost,
    Measurement<long> InputTokens,
    Measurement<long> OutputTokens,
    Measurement<long> CacheTokens,
    Measurement<long> Requests,
    Measurement<long> Steps,
    Measurement<TimeSpan> AgentTime,
    Measurement<TimeSpan> TrialTime);

public sealed record AuditTask(string TaskId, IReadOnlyList<AuditAttempt> Attempts);

public sealed record AuditRun(
    string RunId,
    IReadOnlyList<string> ExpectedTaskIds,
    IReadOnlyList<AuditTask> ObservedTasks);

public sealed record AuditCompleteness(
    int ExpectedTasks,
    int ObservedTasks,
    int MissingTasks,
    int ExpectedAttempts,
    int ObservedAttempts,
    int MissingMeasurements,
    int InvalidMeasurements,
    int PartialMeasurements,
    int ZeroMeasurements)
{
    public bool IsComplete => MissingTasks == 0 && MissingMeasurements == 0 && InvalidMeasurements == 0;
}

public sealed record AuditReport(
    string RunId,
    double PassAt1,
    int PassingAttempts,
    int ObservedAttempts,
    double TaskPassAtLeastOnce,
    int PassingTasks,
    int ObservedTasks,
    double EnterpriseScore,
    double EnterpriseCoverageComponent,
    double EnterprisePassComponent,
    int ExpectedTasks,
    double NewTestCoverage,
    bool ExistingTestRegression,
    double? Cost,
    long? InputTokens,
    long? OutputTokens,
    long? CacheTokens,
    long? Requests,
    long? Steps,
    TimeSpan? AgentTime,
    TimeSpan? TrialTime,
    AuditCompleteness Completeness);

public sealed class AuditContractException(string message) : Exception(message);
