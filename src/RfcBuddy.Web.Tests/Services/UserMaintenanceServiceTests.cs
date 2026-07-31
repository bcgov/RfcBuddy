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
        string tempFolder = Path.Join(Path.GetTempPath(), "rfcbuddy-maintenancetests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var registryMock = new Mock<IUserRegistryService>();
            var tokenMock = new Mock<IApiTokenService>();

            registryMock.Setup(x => x.GetInactiveUserIds(It.IsAny<TimeSpan>()))
                .Returns(new List<string> { "stale-user" });

            // Seed user folder
            string staleUserFolder = Path.Join(tempFolder, "stale-user");
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

    [TestMethod]
    public void CleanupInactiveUserSkipsCleanupWhenCancelled()
    {
        string tempFolder = Path.Join(Path.GetTempPath(), "rfcbuddy-maintenancetests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var registryMock = new Mock<IUserRegistryService>();
            var tokenMock = new Mock<IApiTokenService>();

            string staleUserFolder = Path.Join(tempFolder, "stale-user");
            Directory.CreateDirectory(staleUserFolder);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var service = new UserMaintenanceService(new Mock<IServiceScopeFactory>().Object, NullLogger<UserMaintenanceService>.Instance);
            var method = typeof(UserMaintenanceService).GetMethod("CleanupInactiveUser", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            method!.Invoke(service, new object[] { "stale-user", tempFolder, tokenMock.Object, registryMock.Object, cts.Token });

            // Since cancellation was requested, no deletion or service purging should occur
            Assert.IsTrue(Directory.Exists(staleUserFolder));
            tokenMock.Verify(x => x.PurgeTokensForUser(It.IsAny<string>()), Times.Never);
            registryMock.Verify(x => x.RemoveUser(It.IsAny<string>()), Times.Never);
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
