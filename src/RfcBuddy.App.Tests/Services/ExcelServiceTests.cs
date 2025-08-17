using RfcBuddy.App.Objects;
using System.Data;

namespace RfcBuddy.App.Services.Tests
{
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
    }
}