using RfcBuddy.App.Objects;

namespace RfcBuddy.App.Services.Tests;

[TestClass]
public class RfcChangeTrackerTests
{
    [TestMethod]
    public void GetStatusDetectsNewRfc()
    {
        var tracker = new RfcChangeTracker();
        var current = new Rfc("RFC-1")
        {
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1),
            AssetTags = "tag",
            Description = "desc",
            RiskAssessment = "low"
        };
        var baseline = new List<PreviousRfc>();

        var status = tracker.GetStatus(current, baseline);

        Assert.AreEqual(RfcChangeStatus.New, status);
    }

    [TestMethod]
    public void GetStatusDetectsUnchangedRfc()
    {
        var tracker = new RfcChangeTracker();
        var current = new Rfc("RFC-1")
        {
            StartDate = new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc),
            AssetTags = "tag",
            Description = "desc",
            RiskAssessment = "low"
        };
        var previous = new PreviousRfc("RFC-1")
        {
            StartDate = current.StartDate,
            EndDate = current.EndDate,
            AssetTagsHash = Core.Cryptography.GetSha256Hash(current.AssetTags),
            DescriptionHash = Core.Cryptography.GetSha256Hash(current.Description),
            RiskAssessmentHash = Core.Cryptography.GetSha256Hash(current.RiskAssessment)
        };
        var baseline = new List<PreviousRfc> { previous };

        var status = tracker.GetStatus(current, baseline);

        Assert.AreEqual(RfcChangeStatus.Unchanged, status);
    }

    [TestMethod]
    public void GetStatusDetectsChangedRfc()
    {
        var tracker = new RfcChangeTracker();
        var current = new Rfc("RFC-1")
        {
            StartDate = new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc),
            AssetTags = "tag-new",
            Description = "desc",
            RiskAssessment = "low"
        };
        var previous = new PreviousRfc("RFC-1")
        {
            StartDate = current.StartDate,
            EndDate = current.EndDate,
            AssetTagsHash = Core.Cryptography.GetSha256Hash("tag-old"),
            DescriptionHash = Core.Cryptography.GetSha256Hash(current.Description),
            RiskAssessmentHash = Core.Cryptography.GetSha256Hash(current.RiskAssessment)
        };
        var baseline = new List<PreviousRfc> { previous };

        var status = tracker.GetStatus(current, baseline);

        Assert.AreEqual(RfcChangeStatus.Changed, status);
    }
}
