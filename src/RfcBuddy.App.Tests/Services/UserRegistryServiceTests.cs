using Microsoft.Extensions.Logging.Abstractions;
using RfcBuddy.App.Objects;

namespace RfcBuddy.App.Services.Tests;

[TestClass]
public class UserRegistryServiceTests
{
    [TestMethod]
    public void BootstrapFirstUserAsAdminAndManageAdminStatus()
    {
        string tempFolder = Path.Join(Path.GetTempPath(), "rfcbuddy-userregistrytests", Guid.NewGuid().ToString("N"));
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

            // Non-admin and unknown requesters cannot change administrative status.
            Assert.IsFalse(registry.SetAdmin("user-1", true, "user-1"));
            Assert.IsFalse(registry.SetAdmin("user-1", true, "missing-user"));

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
        string tempFolder = Path.Join(Path.GetTempPath(), "rfcbuddy-userregistrytests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var registry = new UserRegistryService(tempFolder, NullLogger<UserRegistryService>.Instance);

            var user1 = registry.EnsureRegistered("user-1", "John Doe", "john@gov.bc.ca");
            var user2 = registry.EnsureRegistered("user-2", "Jane Smith", "jane@gov.bc.ca");

            // Seed active change-tracking file for user-1
            string user1Folder = Path.Join(tempFolder, "user-1");
            Directory.CreateDirectory(user1Folder);
            string user1File = Path.Join(user1Folder, "PreviousRFCs.txt");
            File.WriteAllText(user1File, "");
            File.SetLastWriteTimeUtc(user1File, DateTime.UtcNow.AddDays(-10));

            // Seed stale change-tracking file for user-2
            string user2Folder = Path.Join(tempFolder, "user-2");
            Directory.CreateDirectory(user2Folder);
            string user2File = Path.Join(user2Folder, "PreviousRFCs.txt");
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

    [TestMethod]
    public void DoesNotMatchExistingUserByDisplayNameHashIdentityOrEmail()
    {
        string tempFolder = Path.Join(Path.GetTempPath(), "rfcbuddy-userregistrytests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            string displayNameHashUserId = RfcBuddy.App.Core.Cryptography.GetSha256Hash("Doe, Jane AG:EX");
            string otherUserId = RfcBuddy.App.Core.Cryptography.GetSha256Hash("attacker");

            var tokenService = new ApiTokenService(tempFolder, NullLogger<ApiTokenService>.Instance);
            tokenService.CreateToken(displayNameHashUserId, "Existing Token", DateTime.UtcNow.AddDays(30));

            var registry = new UserRegistryService(tempFolder, NullLogger<UserRegistryService>.Instance, tokenService);
            var existingUser = registry.EnsureRegistered(displayNameHashUserId, "Doe, Jane AG:EX", "jane.doe@example.com");
            Assert.IsTrue(existingUser.IsAdmin);

            string existingFolder = Path.Join(tempFolder, displayNameHashUserId);
            Directory.CreateDirectory(existingFolder);
            File.WriteAllText(Path.Join(existingFolder, "Keywords.txt"), "gov,forests,general");

            // A different account presenting the same display name and email must not take over the existing record.
            var otherUser = registry.EnsureRegistered(otherUserId, "Doe, Jane AG:EX", "jane.doe@example.com");

            Assert.AreEqual(otherUserId, otherUser.UserId);
            Assert.IsFalse(otherUser.IsAdmin);
            Assert.IsTrue(registry.IsAdmin(displayNameHashUserId));
            Assert.HasCount(2, registry.GetAllUsers());
            Assert.IsTrue(File.Exists(Path.Join(existingFolder, "Keywords.txt")));
            Assert.IsFalse(Directory.Exists(Path.Join(tempFolder, otherUserId)));
            Assert.HasCount(1, tokenService.GetTokensForUser(displayNameHashUserId));
            Assert.IsEmpty(tokenService.GetTokensForUser(otherUserId));
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
    public void RemoveUserRefusesToRemoveLastAdmin()
    {
        string tempFolder = Path.Join(Path.GetTempPath(), "rfcbuddy-userregistrytests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var registry = new UserRegistryService(tempFolder, NullLogger<UserRegistryService>.Instance);
            registry.EnsureRegistered("user-1", "John Doe", "john@gov.bc.ca");
            registry.EnsureRegistered("user-2", "Jane Smith", "jane@gov.bc.ca");
            registry.EnsureRegistered("user-3", "Sam Lee", "sam@gov.bc.ca");
            Assert.IsTrue(registry.SetAdmin("user-2", true, "user-1"));

            // While an admin cleanup is pending, other admins cannot be demoted or removed.
            Assert.IsTrue(registry.MarkUserCleanupPending("user-1"));
            Assert.IsFalse(registry.SetAdmin("user-2", false, "user-1"));
            Assert.IsFalse(registry.RemoveUser("user-2"));

            // Non-admins can be removed, and the pending admin can be removed while another admin remains.
            Assert.IsTrue(registry.RemoveUser("user-3"));
            Assert.IsTrue(registry.RemoveUser("user-1"));

            // The last admin cannot be removed.
            Assert.IsFalse(registry.RemoveUser("user-2"));
            Assert.IsTrue(registry.IsAdmin("user-2"));
            Assert.HasCount(1, registry.GetAllUsers());
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
    public void GetInactiveUserIdsReturnsOldestInactiveFirst()
    {
        string tempFolder = Path.Join(Path.GetTempPath(), "rfcbuddy-userregistrytests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            var registry = new UserRegistryService(tempFolder, NullLogger<UserRegistryService>.Instance);
            registry.EnsureRegistered("user-1", "John Doe", "john@gov.bc.ca");
            registry.EnsureRegistered("user-2", "Jane Smith", "jane@gov.bc.ca");

            SeedActivity(tempFolder, "user-1", DateTime.UtcNow.AddDays(-410));
            SeedActivity(tempFolder, "user-2", DateTime.UtcNow.AddDays(-500));

            var inactive = registry.GetInactiveUserIds(TimeSpan.FromDays(400));
            Assert.HasCount(2, inactive);
            Assert.AreEqual("user-2", inactive[0]);
            Assert.AreEqual("user-1", inactive[1]);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, recursive: true);
            }
        }
    }

    private static void SeedActivity(string dataFolder, string userId, DateTime lastWriteUtc)
    {
        string userFolder = Path.Join(dataFolder, userId);
        Directory.CreateDirectory(userFolder);
        string file = Path.Join(userFolder, "PreviousRFCs.txt");
        File.WriteAllText(file, "");
        File.SetLastWriteTimeUtc(file, lastWriteUtc);
    }
}
