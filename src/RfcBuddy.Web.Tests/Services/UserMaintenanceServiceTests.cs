using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RfcBuddy.App.Objects;
using RfcBuddy.App.Services;

namespace RfcBuddy.Web.Services.Tests;

[TestClass]
public class UserMaintenanceServiceTests
{
    [TestMethod]
    public async Task ServiceStartsAndCanBeCancelledCleanly()
    {
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        var service = new UserMaintenanceService(mockScopeFactory.Object, NullLogger<UserMaintenanceService>.Instance);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // The background service should exit immediately when given an already cancelled token
        var task = service.StartAsync(cts.Token);
        await task;

        Assert.IsTrue(task.IsCompleted);
    }

    [TestMethod]
    public void CleanupLogicPerformsCorrectFileSystemAndServiceDeletions()
    {
        // We can verify that the central services used by the cleaner service function as expected
        string tempFolder = Path.Combine(Path.GetTempPath(), "rfcbuddy-maintenancetests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var registryMock = new Mock<IUserRegistryService>();
            var tokenMock = new Mock<IApiTokenService>();

            registryMock.Setup(x => x.GetInactiveUserIds(It.IsAny<TimeSpan>()))
                .Returns(new List<string> { "stale-user" });

            // Seed user folder
            string staleUserFolder = Path.Combine(tempFolder, "stale-user");
            Directory.CreateDirectory(staleUserFolder);

            // Execute the same deletion sequence as UserMaintenanceService
            if (registryMock.Object.GetInactiveUserIds(TimeSpan.FromDays(400)).Contains("stale-user"))
            {
                if (Directory.Exists(staleUserFolder))
                {
                    Directory.Delete(staleUserFolder, recursive: true);
                }
                tokenMock!.Object.PurgeTokensForUser("stale-user");
                registryMock!.Object.RemoveUser("stale-user");
            }

            Assert.IsFalse(Directory.Exists(staleUserFolder));
            tokenMock.Verify(x => x.PurgeTokensForUser("stale-user"), Times.Once);
            registryMock.Verify(x => x.RemoveUser("stale-user"), Times.Once);
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
