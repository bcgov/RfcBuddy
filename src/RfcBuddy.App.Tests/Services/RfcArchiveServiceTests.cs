using Microsoft.Extensions.Logging.Abstractions;
using RfcBuddy.App.Objects;

namespace RfcBuddy.App.Services.Tests;

[TestClass]
public class RfcArchiveServiceTests
{
    [TestMethod]
    public void UpdateArchiveKeepsLatestVersionPerRfcAndPrunesOldEntries()
    {
        DateTime now = DateTime.Now;
        string tempFolder = Path.Combine(Path.GetTempPath(), "rfcbuddy-archive-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var service = new RfcArchiveService(tempFolder, NullLogger<RfcArchiveService>.Instance);
            service.UpdateArchive([
                new Rfc("RFC-1") { EndDate = now.AddDays(-10) },
                new Rfc("RFC-1") { EndDate = now.AddDays(-5) }
            ]);

            var completed = service.GetCompletedRfcs();
            Assert.AreEqual(1, completed.Count);
            Assert.AreEqual("RFC-1", completed[0].RfcNumber);
            Assert.AreEqual(now.AddDays(-5).Date, completed[0].EndDate.Date);
        }
        finally
        {
            Directory.Delete(tempFolder, true);
        }
    }
}
