using Microsoft.Extensions.Logging.Abstractions;
using RfcBuddy.App.Objects;

namespace RfcBuddy.App.Services.Tests;

[TestClass]
public class UserRegistryServiceTests
{
    [TestMethod]
    public void BootstrapFirstUserAsAdminAndManageAdminStatus()
    {
        string tempFolder = Path.Combine(Path.GetTempPath(), "rfcbuddy-userregistrytests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var registry = new UserRegistryService(tempFolder, NullLogger<UserRegistryService>.Instance);

            // First registered user must automatically be administrator
            var user1 = registry.EnsureRegistered("user-1", "John Doe", "john@gov.bc.ca");
            Assert.IsTrue(user1.IsAdmin);
            Assert.IsTrue(registry.IsAdmin("user-1"));

            // Second user should not be admin
            var user2 = registry.EnsureRegistered("user-2", "Jane Smith", "jane@gov.bc.ca");
            Assert.IsFalse(user2.IsAdmin);
            Assert.IsFalse(registry.IsAdmin("user-2"));

            // Check GetAllUsers
            var allUsers = registry.GetAllUsers();
            Assert.HasCount(2, allUsers);

            // Promote Jane Smith
            Assert.IsTrue(registry.SetAdmin("user-2", true, "user-1"));
            Assert.IsTrue(registry.IsAdmin("user-2"));

            // Attempting to demote user-1 while user-2 is admin should work
            Assert.IsTrue(registry.SetAdmin("user-1", false, "user-2"));
            Assert.IsFalse(registry.IsAdmin("user-1"));

            // Attempting to demote the last remaining admin (user-2) should fail
            Assert.IsFalse(registry.SetAdmin("user-2", false, "user-1"));
            Assert.IsTrue(registry.IsAdmin("user-2")); // Still admin
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
    public void GetInactiveUserIdsIdentifiesInactiveUsers()
    {
        string tempFolder = Path.Combine(Path.GetTempPath(), "rfcbuddy-userregistrytests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var registry = new UserRegistryService(tempFolder, NullLogger<UserRegistryService>.Instance);

            var user1 = registry.EnsureRegistered("user-1", "John Doe", "john@gov.bc.ca");
            var user2 = registry.EnsureRegistered("user-2", "Jane Smith", "jane@gov.bc.ca");

            // Seed active change-tracking file for user-1
            string user1Folder = Path.Combine(tempFolder, "user-1");
            Directory.CreateDirectory(user1Folder);
            string user1File = Path.Combine(user1Folder, "PreviousRFCs.txt");
            File.WriteAllText(user1File, "");
            File.SetLastWriteTimeUtc(user1File, DateTime.UtcNow.AddDays(-10));

            // Seed stale change-tracking file for user-2
            string user2Folder = Path.Combine(tempFolder, "user-2");
            Directory.CreateDirectory(user2Folder);
            string user2File = Path.Combine(user2Folder, "PreviousRFCs.txt");
            File.WriteAllText(user2File, "");
            File.SetLastWriteTimeUtc(user2File, DateTime.UtcNow.AddDays(-410));

            var inactive = registry.GetInactiveUserIds(TimeSpan.FromDays(400));
            Assert.HasCount(1, inactive);
            Assert.AreEqual("user-2", inactive[0]);
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
