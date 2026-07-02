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
            Assert.AreEqual(2, allUsers.Count);

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
            Assert.AreEqual(1, inactive.Count);
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
    public void MigrateOldFormatStoreHandlesExistingRecordsAndMerge()
    {
        string tempFolder = Path.Combine(Path.GetTempPath(), "rfcbuddy-migrationtests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);

        try
        {
            // Seed are old store first
            string usersFile = Path.Combine(tempFolder, "users.json");
            string initialUsersJson = @"[
              {
                ""UserId"": ""66fc729b906e43ecba3d52801c5f99c3@azureidir"",
                ""Identity"": ""Baerike, Christian AG:EX"",
                ""Email"": ""christian.baerike@gov.bc.ca"",
                ""IsAdmin"": true,
                ""FirstSeenUtc"": ""2026-07-02T21:27:58.7026307Z""
              },
              {
                ""UserId"": ""516943ff5126a162c0d257fa2e048cad769dfc89af0c2da7bd6ea70ef0445725"",
                ""Identity"": ""Baerike, Christian AG:EX"",
                ""Email"": ""christian.baerike@gov.bc.ca"",
                ""IsAdmin"": false,
                ""FirstSeenUtc"": ""2026-07-02T23:35:39.0806013Z""
              }
            ]";
            File.WriteAllText(usersFile, initialUsersJson);

            // Also seed tokens representing the old userId and new userId
            string tokensFile = Path.Combine(tempFolder, "apitokens.json");
            string initialTokensJson = @"[
              {
                ""Id"": ""token1"",
                ""OwnerUserId"": ""66fc729b906e43ecba3d52801c5f99c3@azureidir"",
                ""Label"": ""Old Token"",
                ""CreatedUtc"": ""2026-07-02T21:28:00Z"",
                ""ExpiresUtc"": ""2026-10-02T21:28:00Z"",
                ""Revoked"": false,
                ""Hash"": ""somehash""
              }
            ]";
            File.WriteAllText(tokensFile, initialTokensJson);

            // Instantiate service - should trigger migration!
            var registry = new UserRegistryService(tempFolder, NullLogger<UserRegistryService>.Instance);

            var users = registry.GetAllUsers();
            Assert.AreEqual(1, users.Count);
            Assert.AreEqual("516943ff5126a162c0d257fa2e048cad769dfc89af0c2da7bd6ea70ef0445725", users[0].UserId);
            Assert.IsTrue(users[0].IsAdmin); // admin status should merge/preserve

            // Check if tokens were migrated
            string updatedTokensJson = File.ReadAllText(tokensFile);
            Assert.IsTrue(updatedTokensJson.Contains("516943ff5126a162c0d257fa2e048cad769dfc89af0c2da7bd6ea70ef0445725"));
            Assert.IsFalse(updatedTokensJson.Contains("66fc729b906e43ecba3d52801c5f99c3@azureidir"));
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
