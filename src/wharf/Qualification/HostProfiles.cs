using System.Text.Json;

namespace Wharf.Qualification;

public sealed record HostCapacity(long CpuCount, long MemoryBytes);

public sealed record HostProfile(
    string Id,
    string Description,
    int TargetConcurrency,
    long MinimumCpuCount,
    long MinimumMemoryBytes,
    TimeSpan OvernightWindow);

public sealed record ProfileRecommendation(
    string ProfileId,
    int OperatorLimit,
    int RecommendedConcurrency,
    int RecommendedBuildConcurrency,
    bool RaisedOperatorLimit,
    IReadOnlyList<string> Reasons);

public sealed record QualificationEvidence(
    string ProfileId,
    string HardwareDescription,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    int ExpectedTasks,
    int CompletedTasks,
    int LostCompletedTasks,
    TimeSpan Deadline,
    double? Cost,
    bool ScoreComplete,
    string EvidencePath)
{
    public bool IsQualified =>
        ExpectedTasks > 0 &&
        CompletedTasks == ExpectedTasks &&
        LostCompletedTasks == 0 &&
        FinishedAt - StartedAt <= Deadline &&
        ScoreComplete;
}

public sealed record QualificationReport(
    string SchemaVersion,
    IReadOnlyList<HostProfile> Profiles,
    IReadOnlyList<QualificationEvidence> Evidence,
    IReadOnlyList<ProfileRecommendation> Recommendations);

public static class HostProfiles
{
    public const string SchemaVersion = "1.0";

    public static IReadOnlyList<HostProfile> Published { get; } =
    [
        new("on-prem-32", "32-concurrent overnight profile", 32, 16, 64L * 1024 * 1024 * 1024, TimeSpan.FromHours(12)),
        new("on-prem-88", "88-concurrent overnight profile", 88, 64, 256L * 1024 * 1024 * 1024, TimeSpan.FromHours(12))
    ];

    public static HostProfile Get(string id) =>
        Published.SingleOrDefault(profile => profile.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ??
        throw new QualificationException($"unknown host profile: {id}");
}

public sealed class HostProfileService
{
    public ProfileRecommendation Recommend(
        HostProfile profile,
        HostCapacity capacity,
        int operatorLimit,
        int operatorBuildLimit)
    {
        if (operatorLimit < 1 || operatorBuildLimit < 1)
            throw new QualificationException("operator limits must be positive");

        int resourceLimit = (int)Math.Min(
            Math.Min(profile.TargetConcurrency, Math.Max(1, capacity.CpuCount * 2)),
            Math.Max(1, capacity.MemoryBytes / (2L * 1024 * 1024 * 1024)));
        int recommendation = Math.Min(operatorLimit, resourceLimit);
        int buildRecommendation = Math.Min(operatorBuildLimit, Math.Max(1, (int)capacity.CpuCount));
        List<string> reasons =
        [
            $"operator trial limit={operatorLimit}",
            $"operator build limit={operatorBuildLimit}",
            $"resource cap={resourceLimit}"
        ];
        if (operatorLimit < profile.TargetConcurrency)
            reasons.Add("operator limit is below the published target");
        if (recommendation < profile.TargetConcurrency)
            reasons.Add("host capacity is below the published target");

        return new ProfileRecommendation(
            profile.Id,
            operatorLimit,
            recommendation,
            buildRecommendation,
            RaisedOperatorLimit: false,
            reasons);
    }

    public QualificationReport CreateReport(
        IEnumerable<QualificationEvidence> evidence,
        IEnumerable<ProfileRecommendation> recommendations)
    {
        QualificationEvidence[] retained = evidence.OrderBy(item => item.ProfileId, StringComparer.Ordinal).ToArray();
        ProfileRecommendation[] selected = recommendations.OrderBy(item => item.ProfileId, StringComparer.Ordinal).ToArray();
        return new QualificationReport(HostProfiles.SchemaVersion, HostProfiles.Published, retained, selected);
    }

    public static void WriteReport(string path, QualificationReport report)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new QualificationException("qualification report path is required");
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }));
    }
}

public sealed class QualificationException(string message) : Exception(message);
