using Wharf.Copilot;

namespace Wharf.Agent;

public sealed record AgentExecutionRequest(
    string TaskId,
    string AgentImage,
    string SourceDirectory,
    string WorkspaceDirectory,
    string Network,
    CopilotInvocation Invocation,
    IReadOnlyList<string> HiddenPaths,
    string? ContainerName = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TaskId) ||
            string.IsNullOrWhiteSpace(AgentImage) ||
            string.IsNullOrWhiteSpace(Network))
        {
            throw new AgentExecutionException("task, agent image, and network are required");
        }

        ValidateDirectory(SourceDirectory, nameof(SourceDirectory));
        ValidateDirectory(WorkspaceDirectory, nameof(WorkspaceDirectory));
        string source = Path.GetFullPath(SourceDirectory);
        string workspace = Path.GetFullPath(WorkspaceDirectory);
        if (source.Equals(workspace, StringComparison.Ordinal))
        {
            throw new AgentExecutionException("source and agent workspace must be separate");
        }

        foreach (string hiddenPath in HiddenPaths)
        {
            string fullPath = Path.GetFullPath(hiddenPath);
            if (IsWithin(fullPath, source) || IsWithin(fullPath, workspace))
            {
                throw new AgentExecutionException(
                    $"hidden verifier material cannot be mounted into the agent: {hiddenPath}");
            }
        }
    }

    private static void ValidateDirectory(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new AgentExecutionException($"{name} must be an absolute path");
        }

        if (!Directory.Exists(path))
        {
            throw new AgentExecutionException($"{name} does not exist: {path}");
        }
    }

    private static bool IsWithin(string path, string parent)
    {
        string normalizedParent = parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedParent, StringComparison.Ordinal);
    }
}

public sealed record AgentRunResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    string Trajectory,
    string Patch,
    TimeSpan Duration,
    int? Tokens = null,
    int? Steps = null,
    int? Requests = null,
    decimal? Cost = null,
    string? DebugLogDirectory = null,
    AgentUsage? Usage = null,
    bool OutOfMemory = false);

public sealed record AgentModelUsage(
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    long ReasoningTokens,
    int Requests,
    decimal RequestCost);

public sealed record AgentUsage(
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    long ReasoningTokens,
    int Requests,
    decimal RequestCost,
    decimal? AiCredits,
    IReadOnlyDictionary<string, AgentModelUsage> Models)
{
    public long TotalTokens => InputTokens + OutputTokens;
}

public sealed record AgentEvidenceSummary(
    string SchemaVersion,
    string TaskId,
    string Status,
    int ExitCode,
    long DurationMilliseconds,
    int? Tokens,
    int? Steps,
    int? Requests,
    decimal? Cost,
    string PatchPath,
    string StandardOutputPath,
    string StandardErrorPath,
    string TrajectoryPath,
    AgentUsage? Usage = null,
    string? ErrorType = null,
    string? Error = null);

public sealed record AgentExecutionOutcome(
    AgentEvidenceSummary Summary,
    AgentExecutionRequest Request,
    decimal? TotalCost,
    int InfrastructureRetries);

public interface IAgentRunner
{
    Task<AgentRunResult> RunAsync(
        AgentExecutionRequest request,
        CancellationToken cancellationToken);
}

public interface IAgentWorkspacePreparer
{
    Task PrepareAsync(
        AgentExecutionRequest request,
        CancellationToken cancellationToken);
}

public sealed class AgentExecutionException(string message) : Exception(message);

public static class AgentEvidenceSchema
{
    public const string Version = "1.1";
}
