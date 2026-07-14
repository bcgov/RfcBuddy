using Microsoft.Extensions.Logging.Abstractions;
using RfcBuddy.App.Objects;

namespace RfcBuddy.App.Services.Tests;

[TestClass]
public class RfcArchiveServiceTests
{
    public TestContext TestContext { get; set; } = null!;

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
            Assert.HasCount(1, completed);
            Assert.AreEqual("RFC-1", completed[0].RfcNumber);
            Assert.AreEqual(now.AddDays(-5).Date, completed[0].EndDate.Date);

        }
        finally
        {
            Directory.Delete(tempFolder, true);
        }
    }

    [TestMethod]
    public void UpdateArchivePreservesLatestVersionAcrossConcurrentUpdates()
    {
        string tempFolder = Path.Combine(Path.GetTempPath(), "rfcbuddy-archive-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var service = new RfcArchiveService(tempFolder, NullLogger<RfcArchiveService>.Instance);
            var startGate = new ManualResetEventSlim(false);

            for (int iteration = 0; iteration < 25; iteration++)
            {
                var archiveFile = Path.Combine(tempFolder, "archived-rfcs.json");
                if (File.Exists(archiveFile))
                {
                    File.Delete(archiveFile);
                }

                startGate.Reset();
                CancellationToken token = TestContext.CancellationToken;

                Task[] tasks =
                [
                    Task.Run(() =>
                    {
                        startGate.Wait(token);
                        service.UpdateArchive([new Rfc("RFC-1") { EndDate = DateTime.Today.AddDays(-5) }]);
                    }, token),
                    Task.Run(() =>
                    {
                        startGate.Wait(token);
                        service.UpdateArchive([new Rfc("RFC-1") { EndDate = DateTime.Today.AddDays(-3) }]);
                    }, token)
                ];

                startGate.Set();
                Task.WaitAll(tasks, token);

                List<Rfc> completed = service.GetCompletedRfcs();
                Assert.HasCount(1, completed);
                Assert.AreEqual("RFC-1", completed[0].RfcNumber, $"Iteration {iteration} should keep RFC-1.");
                Assert.AreEqual(DateTime.Today.AddDays(-3).Date, completed[0].EndDate.Date, $"Iteration {iteration} should retain the latest end date.");
            }
        }
        finally
        {
            Directory.Delete(tempFolder, true);
        }
    }
}
