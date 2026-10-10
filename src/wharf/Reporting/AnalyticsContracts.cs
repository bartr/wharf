using System.Text.Json.Serialization;

namespace Wharf.Reporting;

public sealed record AnalyticsExportOptions(
    string Arm,
    string Agent,
    string? AgentVersion,
    string Model,
    string Effort,
    bool Subagents,
    string? Job = null);

public sealed record AnalyticsTrial(
    [property: JsonPropertyName("arm")] string Arm,
    [property: JsonPropertyName("job")] string Job,
    [property: JsonPropertyName("trial")] string Trial,
    [property: JsonPropertyName("task_id")] string TaskId,
    [property: JsonPropertyName("language")] string? Language,
    [property: JsonPropertyName("agent")] string Agent,
    [property: JsonPropertyName("agent_version")] string? AgentVersion,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("effort")] string Effort,
    [property: JsonPropertyName("subagents")] bool Subagents,
    [property: JsonPropertyName("reward")] double? Reward,
    [property: JsonPropertyName("f2p_passed")] int? FunctionalTestsPassed,
    [property: JsonPropertyName("f2p_total")] int? FunctionalTestsTotal,
    [property: JsonPropertyName("p2p_passed")] int? RegressionTestsPassed,
    [property: JsonPropertyName("p2p_total")] int? RegressionTestsTotal,
    [property: JsonPropertyName("cost_usd")] decimal? CostUsd,
    [property: JsonPropertyName("credits")] decimal? Credits,
    [property: JsonPropertyName("premium_requests")] int? PremiumRequests,
    [property: JsonPropertyName("n_input_tokens")] long? InputTokens,
    [property: JsonPropertyName("n_cache_tokens")] long? CacheTokens,
    [property: JsonPropertyName("n_output_tokens")] long? OutputTokens,
    [property: JsonPropertyName("n_agent_steps")] int? AgentSteps,
    [property: JsonPropertyName("peak_context_tokens")] long? PeakContextTokens,
    [property: JsonPropertyName("agent_seconds")] double? AgentSeconds,
    [property: JsonPropertyName("trial_seconds")] double? TrialSeconds,
    [property: JsonPropertyName("patch_bytes")] long? PatchBytes,
    [property: JsonPropertyName("exception_type")] string? ExceptionType,
    [property: JsonPropertyName("task_checksum")] string? TaskChecksum);

public sealed record DataManifestEntry(
    [property: JsonPropertyName("arm")] string Arm,
    [property: JsonPropertyName("bytes_jsonl")] long BytesJsonl,
    [property: JsonPropertyName("scored")] int Scored,
    [property: JsonPropertyName("tasks")] int Tasks,
    [property: JsonPropertyName("total_cost_usd")] decimal? TotalCostUsd,
    [property: JsonPropertyName("trials")] int Trials,
    [property: JsonPropertyName("with_cost")] int WithCost);

public sealed class AnalyticsEvidenceException(string message) : Exception(message);
