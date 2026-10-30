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
            registryMock.Setup(x => x.MarkUserCleanupPending("stale-user")).Returns(true);
            registryMock.Setup(x => x.RemoveUser("stale-user")).Returns(true);

            // Seed user folder
            string staleUserFolder = Path.Join(tempFolder, "stale-user");
            Directory.CreateDirectory(staleUserFolder);

            InvokeCleanup("stale-user", tempFolder, tokenMock.Object, registryMock.Object, CancellationToken.None);

            Assert.IsFalse(Directory.Exists(staleUserFolder));
            registryMock.Verify(x => x.MarkUserCleanupPending("stale-user"), Times.Once);
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
            registryMock.Verify(x => x.MarkUserCleanupPending(It.IsAny<string>()), Times.Never);
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

    [TestMethod]
    public void CleanupInactiveUserKeepsLastAdminData()
    {
        string tempFolder = Path.Join(Path.GetTempPath(), "rfcbuddy-maintenancetests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var tokenService = new ApiTokenService(tempFolder, NullLogger<ApiTokenService>.Instance);
            var registry = new UserRegistryService(tempFolder, NullLogger<UserRegistryService>.Instance, tokenService);
            registry.EnsureRegistered("admin-user", "Admin User", "admin@gov.bc.ca");
            tokenService.CreateToken("admin-user", "Admin Token", DateTime.UtcNow.AddDays(30));
            string adminFolder = Path.Join(tempFolder, "admin-user");
            Directory.CreateDirectory(adminFolder);

            InvokeCleanup("admin-user", tempFolder, tokenService, registry, CancellationToken.None);

            Assert.IsTrue(registry.IsAdmin("admin-user"));
            Assert.IsTrue(Directory.Exists(adminFolder));
            Assert.HasCount(1, tokenService.GetTokensForUser("admin-user"));
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
    public void CleanupInactiveUserRemovesUserWhenNotLastAdmin()
    {
        string tempFolder = Path.Join(Path.GetTempPath(), "rfcbuddy-maintenancetests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var tokenService = new ApiTokenService(tempFolder, NullLogger<ApiTokenService>.Instance);
            var registry = new UserRegistryService(tempFolder, NullLogger<UserRegistryService>.Instance, tokenService);
            registry.EnsureRegistered("admin-user", "Admin User", "admin@gov.bc.ca");
            registry.EnsureRegistered("stale-user", "Stale User", "stale@gov.bc.ca");
            tokenService.CreateToken("stale-user", "Stale Token", DateTime.UtcNow.AddDays(30));
            string staleFolder = Path.Join(tempFolder, "stale-user");
            Directory.CreateDirectory(staleFolder);

            InvokeCleanup("stale-user", tempFolder, tokenService, registry, CancellationToken.None);

            Assert.HasCount(1, registry.GetAllUsers());
            Assert.IsFalse(Directory.Exists(staleFolder));
            Assert.IsEmpty(tokenService.GetTokensForUser("stale-user"));
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
    public void CleanupFailureKeepsPendingUserForRetry()
    {
        string tempFolder = Path.Join(Path.GetTempPath(), "rfcbuddy-maintenancetests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var registry = new UserRegistryService(tempFolder, NullLogger<UserRegistryService>.Instance);
            registry.EnsureRegistered("admin-user", "Admin User", "admin@gov.bc.ca");
            registry.EnsureRegistered("stale-user", "Stale User", "stale@gov.bc.ca");
            string staleFolder = Path.Join(tempFolder, "stale-user");
            Directory.CreateDirectory(staleFolder);
            var tokenService = new Mock<IApiTokenService>();
            tokenService.Setup(x => x.PurgeTokensForUser("stale-user"))
                .Throws(new IOException("Simulated token-store failure."));

            InvokeCleanup("stale-user", tempFolder, tokenService.Object, registry, CancellationToken.None);

            Assert.IsTrue(Directory.Exists(staleFolder));
            Assert.HasCount(2, registry.GetAllUsers());
            Assert.AreEqual("stale-user", registry.GetInactiveUserIds(TimeSpan.FromDays(400)).Single());
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, recursive: true);
            }
        }
    }

    private static void InvokeCleanup(string userId, string dataFolder, IApiTokenService tokenService, IUserRegistryService registry, CancellationToken token)
    {
        var service = new UserMaintenanceService(new Mock<IServiceScopeFactory>().Object, NullLogger<UserMaintenanceService>.Instance);
        var method = typeof(UserMaintenanceService).GetMethod("CleanupInactiveUser", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method!.Invoke(service, [userId, dataFolder, tokenService, registry, token]);
    }
}
