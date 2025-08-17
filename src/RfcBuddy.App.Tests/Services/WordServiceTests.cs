using RfcBuddy.App.Objects;

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
        wordService.CreateWordFile(ref stream, ministryRfcs, generalRfcs, otherRfcs, previousRfcs);
        Assert.IsTrue(stream.Length > 0);
    }
}