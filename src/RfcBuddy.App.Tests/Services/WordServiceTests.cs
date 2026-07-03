using RfcBuddy.App.Objects;
using Xceed.Words.NET;

namespace RfcBuddy.App.Services.Tests;

[TestClass()]
public class WordServiceTests
{
    [TestMethod()]
    public void CreateWordFileTest()
    {
        Stream stream = new MemoryStream();
        WordService wordService = new();
        List<Rfc> ministryRfcs = [];
        List<Rfc> generalRfcs = [];
        List<Rfc> otherRfcs = [];
        List<PreviousRfc> previousRfcs = [];
        Assert.IsTrue(stream.Length == 0);
        wordService.CreateWordFile(ref stream, ministryRfcs, generalRfcs, otherRfcs, previousRfcs, [], [], []);
        Assert.IsTrue(stream.Length > 0);
    }

    [TestMethod()]
    public void CreateWordFileIncludesCompletedSectionWhenCompletedRfcsExist()
    {
        Stream stream = new MemoryStream();
        WordService wordService = new();
        List<Rfc> ministryRfcs = [];
        List<Rfc> generalRfcs = [];
        List<Rfc> otherRfcs = [];
        List<PreviousRfc> previousRfcs = [];
        List<Rfc> completedMinistryRfcs =
        [
            new("CHG001") { StartDate = DateTime.Now.AddDays(-30), EndDate = DateTime.Now.AddDays(-2), ApprovalStatus = "Approved", Platform = "Windows" }
        ];

        wordService.CreateWordFile(ref stream, ministryRfcs, generalRfcs, otherRfcs, previousRfcs, completedMinistryRfcs, [], []);
        stream.Position = 0;

        using DocX document = DocX.Load(stream);
        string paragraphs = string.Join(" ", document.Paragraphs.Select(x => x.Text));

        StringAssert.Contains(paragraphs, "Completed (last 5 weeks)");
        StringAssert.Contains(paragraphs, "CHG001");
    }
}