using Wharf.Qualification;

namespace Wharf.Tests;

public sealed class HostProfileTests
{
    [Fact]
    public void RecommendationNeverRaisesOperatorLimit()
    {
        HostProfile profile = HostProfiles.Get("on-prem-88");
        ProfileRecommendation recommendation = new HostProfileService().Recommend(
            profile,
            new HostCapacity(128, 512L * 1024 * 1024 * 1024),
            operatorLimit: 12,
            operatorPreparationLimit: 4);

        Assert.Equal(12, recommendation.RecommendedConcurrency);
        Assert.Equal(4, recommendation.RecommendedPreparationConcurrency);
        Assert.False(recommendation.RaisedOperatorLimit);
    }

    [Fact]
    public void ResourceCapacityCapsPublishedTarget()
    {
        ProfileRecommendation recommendation = new HostProfileService().Recommend(
            HostProfiles.Get("on-prem-32"),
            new HostCapacity(8, 16L * 1024 * 1024 * 1024),
            operatorLimit: 32,
            operatorPreparationLimit: 16);

        Assert.Equal(8, recommendation.RecommendedConcurrency);
        Assert.Contains("host capacity is below the published target", recommendation.Reasons);
    }

    [Fact]
    public void IncompleteEvidenceIsNotQualified()
    {
        QualificationEvidence evidence = new(
            "on-prem-32", "fixture hardware",
            DateTimeOffset.Parse("2026-10-06T00:00:00-05:00"),
            DateTimeOffset.Parse("2026-10-06T13:00:00-05:00"),
            100, 100, 0, TimeSpan.FromHours(12), 1, true, "fixture.json");

        Assert.False(evidence.IsQualified);
    }
}
