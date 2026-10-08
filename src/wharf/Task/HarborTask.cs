namespace Wharf.Tasks;

public sealed record HarborTask(
    string TaskId,
    string Name,
    string Description,
    string DisplayTitle,
    string Category,
    string Language,
    string SchemaVersion,
    string RootPath,
    string InstructionPath,
    string SourceChecksum,
    int? Cpus,
    int? MemoryMb,
    int? StorageMb,
    int? Gpus,
    double? AgentBuildTimeoutSeconds,
    double? AgentTimeoutSeconds,
    double? VerifierBuildTimeoutSeconds,
    double? VerifierTimeoutSeconds,
    string? EnvironmentImage = null,
    double? EstimatedAgentDurationSeconds = null,
    double? EstimatedVerifierDurationSeconds = null,
    double? SchedulingDurationSeconds = null);

public sealed record TaskSelection(
    string? Language = null,
    string? Include = null,
    string? Exclude = null,
    int? Sample = null,
    int Seed = 0,
    int Attempts = 1,
    string? Shard = null);

public sealed record TaskAttempt(
    string AttemptId,
    HarborTask Task,
    int AttemptNumber);

public sealed class HarborTaskException(string message) : Exception(message);
