using System.Security.Principal;
using RfcBuddy.App.Objects;

namespace RfcBuddy.App.Services.Tests;

[TestClass]
public class UserServiceTests
{
    private sealed class FakePrincipal : IPrincipal
    {
        public IIdentity? Identity { get; } = new GenericIdentity("TestUser");
        public bool IsInRole(string role) => false;
    }

    [TestMethod]
    public void GetAndSavePreviousRfcsRoundTrip()
    {
        string tempFolder = Path.Combine(Path.GetTempPath(), "rfcbuddy-userservicetests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var appSettingsService = new FakeAppSettingsService();
            appSettingsService.AppSettings.DataFolder = tempFolder;
            var principal = new FakePrincipal();

            var userService = new UserService(appSettingsService, principal);

            var rfcs = new List<Rfc>
            {
                new("RFC-123")
                {
                    StartDate = new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc),
                    EndDate = new DateTime(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc),
                    AssetTags = "payments",
                    Description = "payment desc",
                    RiskAssessment = "low"
                }
            };

            userService.SavePreviousRfcs(rfcs);
            var previous = userService.GetPreviousRfcs();

            Assert.AreEqual(1, previous.Count);
            Assert.AreEqual("RFC-123", previous[0].RfcNumber);
            Assert.AreEqual(rfcs[0].StartDate, previous[0].StartDate);
            Assert.AreEqual(rfcs[0].EndDate, previous[0].EndDate);

            // API scope previous rfcs
            userService.SavePreviousRfcs(rfcs, BaselineScope.Api);
            var apiPrevious = userService.GetPreviousRfcs(BaselineScope.Api);

            Assert.AreEqual(1, apiPrevious.Count);
            Assert.AreEqual("RFC-123", apiPrevious[0].RfcNumber);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, recursive: true);
            }
        }
    }

    [TestMethod]
    public void GetAndSaveUserKeywordsRoundTrip()
    {
        string tempFolder = Path.Combine(Path.GetTempPath(), "rfcbuddy-userservicetests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var appSettingsService = new FakeAppSettingsService();
            appSettingsService.AppSettings.DataFolder = tempFolder;
            var principal = new FakePrincipal();

            var userService = new UserService(appSettingsService, principal);

            userService.SaveUserKeywords(["ministry"], ["general1", "general2"], ["ignore"]);

            userService.GetUserKeywords(out var ministry, out var general, out var ignore);

            CollectionAssert.AreEqual(new List<string> { "ministry" }, ministry);
            CollectionAssert.AreEqual(new List<string> { "general1", "general2" }, general);
            CollectionAssert.AreEqual(new List<string> { "ignore" }, ignore);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, recursive: true);
            }
        }
    }
}
