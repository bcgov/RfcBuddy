using RfcBuddy.App.Objects;
using System.Data;

namespace RfcBuddy.App.Services.Tests
{
    internal sealed class FakeAppSettingsService : IAppSettingsService
    {
        public AppSettings AppSettings { get; } = new();
    }

    [TestClass()]
    public class ExcelServiceTests
    {
        [TestMethod()]
        public void ReadRfcTest()
        {
            DataTable dt = new();
            dt.Columns.Add("RfcNo");
            dt.Columns.Add("Approval");
            dt.Columns.Add("Platform");
            dt.Columns.Add("AssetTag");
            dt.Columns.Add("StartDate");
            dt.Columns.Add("EndDate");
            dt.Columns.Add("Description");
            dt.Columns.Add("Risk");
            object[] data = new[] { "CHG0072560", "Approved", "Windows", "starsky, hutch", "2024 - 03 - 18  1:00:00 PM", "2024-03-25  1:00:00 PM", "Standard Change 049", "Low risk, this is a routine process that is repeated many times." };
            dt.LoadDataRow(data, true);
            Rfc rfc = ExcelService.ReadRfc(ref dt, 0);
            Assert.AreEqual("CHG0072560", rfc.RfcNumber);
            Assert.AreEqual("Approved", rfc.ApprovalStatus);
        }

        [TestMethod()]
        [DataRow(true, "Tag2", "Foo")]
        [DataRow(true, "tag2", "Foo")]  //Matches should be case-insensitive
        [DataRow(true, "Bla", "Description")]
        [DataRow(true, "No risk")]  //Spaces allowed within keywords
        [DataRow(false, "Foo", "Bar")]
        [DataRow(false, "Tag2 ")]  //Should not match with a trailing space
        [DataRow(false, " RFC")]  //Should not match with a leading space either
        public void RfcKeywordMatchesTest(bool expected, params string[] keywords)
        {
            Rfc rfc = new("123456")
            {
                AssetTags = "Tag1 ,Tag2,Tag3",
                Description = "RFC description",
                RiskAssessment = "No risk to assets",
            };
            var actual = ExcelService.RfcKeywordMatches(ref rfc, [.. keywords]);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod()]
        public void RfcKeywordAddedToRfc()
        {
            Rfc rfc = new("123456")
            {
                AssetTags = "Tag1 ,Tag2,Tag3",
                Description = "RFC description",
                RiskAssessment = "No risk to assets",
            };
            Assert.AreEqual(0, rfc.Keywords.Count);
            List<string> keywords = ["Tag2", "Foo"];
            _ = ExcelService.RfcKeywordMatches(ref rfc, keywords);
            Assert.AreEqual(1, rfc.Keywords.Count);
            Assert.AreEqual("Tag2", rfc.Keywords[0]);
        }

        [TestMethod()]
        public void CategorizeRfcsSplitsRfcsAccordingToKeywords()
        {
            ExcelService excelService = new(new FakeAppSettingsService());
            List<Rfc> rfcs =
            [
                new("1") { AssetTags = "Tag2", Description = "Example", RiskAssessment = "Low" },
                new("2") { AssetTags = "General item", Description = "Example", RiskAssessment = "Low" },
                new("3") { AssetTags = "Unrelated", Description = "Example", RiskAssessment = "Low" }
            ];

            excelService.CategorizeRfcs(rfcs, ["Tag2"], ["General"], ["Ignore"], out List<Rfc> ministryRfcs, out List<Rfc> generalRfcs, out List<Rfc> otherRfcs);

            Assert.AreEqual(1, ministryRfcs.Count);
            Assert.AreEqual("1", ministryRfcs[0].RfcNumber);
            Assert.AreEqual(1, generalRfcs.Count);
            Assert.AreEqual("2", generalRfcs[0].RfcNumber);
            Assert.AreEqual(1, otherRfcs.Count);
            Assert.AreEqual("3", otherRfcs[0].RfcNumber);
        }
    }
}