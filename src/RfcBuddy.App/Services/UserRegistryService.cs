using Microsoft.Extensions.Logging;
using RfcBuddy.App.Objects;
using System.Collections.Concurrent;
using System.Text.Json;

namespace RfcBuddy.App.Services;

public interface IUserRegistryService
{
    UserRecord EnsureRegistered(string userId, string identity, string email = "");
    bool IsAdmin(string userId);
    IReadOnlyList<UserListEntry> GetAllUsers();
    bool SetAdmin(string targetUserId, bool isAdmin, string requestingAdminUserId);
    void RemoveUser(string userId);
    IReadOnlyList<string> GetInactiveUserIds(TimeSpan threshold);
}

public sealed class UserListEntry
{
    public required string UserId { get; init; }
    public required string Identity { get; init; }
    public required string Email { get; init; }
    public required bool IsAdmin { get; init; }
    public required DateTime LastActiveUtc { get; init; }
}

public sealed class UserRegistryService : IUserRegistryService
{
    private static readonly ConcurrentDictionary<string, System.Threading.Mutex> lockRegistry = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Threading.Mutex _writeMutex;
    private readonly string _dataFolder;
    private readonly string _storePath;
    private readonly ILogger<UserRegistryService> _logger;

    public UserRegistryService(string dataFolder, ILogger<UserRegistryService> logger)
    {
        _dataFolder = Path.GetFullPath(dataFolder);
        _storePath = Path.Combine(_dataFolder, "users.json");
        _logger = logger;
        _writeMutex = lockRegistry.GetOrAdd(_storePath, static path => new System.Threading.Mutex(false, "Global\\RfcBuddyUsers_" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path)))));
        Directory.CreateDirectory(_dataFolder);

        MigrateOldFormatStore();
    }

    public UserRecord EnsureRegistered(string userId, string identity, string email = "")
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        _writeMutex.WaitOne();
        try
        {
            List<UserRecord> users = LoadStore();
            UserRecord? existing = users.FirstOrDefault(x => string.Equals(x.UserId, userId, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.Identity = identity;
                if (!string.IsNullOrEmpty(email))
                {
                    existing.Email = email;
                }
                SaveStore(users);
                return existing;
            }

            bool hasAdmin = users.Any(x => x.IsAdmin);
            var user = new UserRecord
            {
                UserId = userId,
                Identity = identity,
                Email = email,
                IsAdmin = !hasAdmin,
                FirstSeenUtc = DateTime.UtcNow
            };
            users.Add(user);
            SaveStore(users);
            return user;
        }
        finally
        {
            _writeMutex.ReleaseMutex();
        }
    }

    public bool IsAdmin(string userId)
    {
        _writeMutex.WaitOne();
        try
        {
            return LoadStore().Any(x => string.Equals(x.UserId, userId, StringComparison.OrdinalIgnoreCase) && x.IsAdmin);
        }
        finally
        {
            _writeMutex.ReleaseMutex();
        }
    }

    public IReadOnlyList<UserListEntry> GetAllUsers()
    {
        _writeMutex.WaitOne();
        try
        {
            List<UserRecord> users = LoadStore();
            return [.. users.Select(x => new UserListEntry
            {
                UserId = x.UserId,
                Identity = x.Identity,
                Email = x.Email,
                IsAdmin = x.IsAdmin,
                LastActiveUtc = GetLastActiveUtc(x.UserId)
            })];
        }
        finally
        {
            _writeMutex.ReleaseMutex();
        }
    }

    public bool SetAdmin(string targetUserId, bool isAdmin, string requestingAdminUserId)
    {
        _writeMutex.WaitOne();
        try
        {
            List<UserRecord> users = LoadStore();
            UserRecord? target = users.FirstOrDefault(x => string.Equals(x.UserId, targetUserId, StringComparison.OrdinalIgnoreCase));
            if (target is null)
            {
                return false;
            }

            if (!isAdmin && !users.Any(x => x.IsAdmin && !string.Equals(x.UserId, targetUserId, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            target.IsAdmin = isAdmin;
            SaveStore(users);
            return true;
        }
        finally
        {
            _writeMutex.ReleaseMutex();
        }
    }

    public void RemoveUser(string userId)
    {
        _writeMutex.WaitOne();
        try
        {
            List<UserRecord> users = LoadStore();
            users.RemoveAll(x => string.Equals(x.UserId, userId, StringComparison.OrdinalIgnoreCase));
            SaveStore(users);
        }
        finally
        {
            _writeMutex.ReleaseMutex();
        }
    }

    public IReadOnlyList<string> GetInactiveUserIds(TimeSpan threshold)
    {
        _writeMutex.WaitOne();
        try
        {
            List<UserRecord> users = LoadStore();
            DateTime cutoff = DateTime.UtcNow.Subtract(threshold);
            return [.. users.Where(x => GetLastActiveUtc(x.UserId) <= cutoff).Select(x => x.UserId)];
        }
        finally
        {
            _writeMutex.ReleaseMutex();
        }
    }

    private DateTime GetLastActiveUtc(string userId)
    {
        string userFolder = Path.Combine(_dataFolder, userId);
        DateTime latest = DateTime.MinValue;
        foreach (string filePath in new[] { Path.Combine(userFolder, "PreviousRFCs.txt"), Path.Combine(userFolder, "ApiPreviousRFCs.txt") })
        {
            if (File.Exists(filePath))
            {
                latest = latest > File.GetLastWriteTimeUtc(filePath) ? latest : File.GetLastWriteTimeUtc(filePath);
            }
        }

        if (latest == DateTime.MinValue)
        {
            return DateTime.MinValue;
        }

        return latest;
    }

    private List<UserRecord> LoadStore()
    {
        if (!File.Exists(_storePath))
        {
            return [];
        }

        try
        {
            string json = File.ReadAllText(_storePath);
            return JsonSerializer.Deserialize<List<UserRecord>>(json) ?? [];
        }
        catch (JsonException)
        {
            _logger.LogWarning("User store was corrupted. Resetting it.");
            return [];
        }
    }

    private void SaveStore(IEnumerable<UserRecord> users)
    {
        string json = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
        string tempPath = Path.Combine(_dataFolder, $"users.json.tmp-{Guid.NewGuid():N}");
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _storePath, true);
    }

    private void MigrateOldFormatStore()
    {
        _writeMutex.WaitOne();
        try
        {
            if (!File.Exists(_storePath))
            {
                return;
            }

            List<UserRecord> users = LoadStore();
            bool modified = false;

            // Find all user records where UserId is not a SHA-256 hash (64 hex characters)
            List<UserRecord> oldRecords = users.Where(u => !IsSha256Hash(u.UserId)).ToList();

            foreach (UserRecord oldRecord in oldRecords)
            {
                if (string.IsNullOrWhiteSpace(oldRecord.Identity))
                {
                    continue;
                }

                string originalOldUserId = oldRecord.UserId;
                string newUserId = RfcBuddy.App.Core.Cryptography.GetSha256Hash(oldRecord.Identity);
                _logger.LogInformation("Migrating old user format: {OldUserId} -> {NewUserId} ({Identity})", originalOldUserId, newUserId, oldRecord.Identity);

                // See if a new record with this hashed ID already exists
                UserRecord? newRecord = users.FirstOrDefault(u => string.Equals(u.UserId, newUserId, StringComparison.OrdinalIgnoreCase));
                if (newRecord is not null)
                {
                    // Merge old record properties into new record
                    newRecord.IsAdmin = newRecord.IsAdmin || oldRecord.IsAdmin;
                    if (oldRecord.FirstSeenUtc < newRecord.FirstSeenUtc)
                    {
                        newRecord.FirstSeenUtc = oldRecord.FirstSeenUtc;
                    }
                    if (!string.IsNullOrEmpty(oldRecord.Email) && string.IsNullOrEmpty(newRecord.Email))
                    {
                        newRecord.Email = oldRecord.Email;
                    }
                    users.Remove(oldRecord);
                }
                else
                {
                    // Convert old record to new UserId
                    oldRecord.UserId = newUserId;
                }

                // Also migrate physical directories if an unhashed user directory exists
                MigrateUserDirectory(originalOldUserId, newUserId);

                // Also migrate tokens in apitokens.json for this user!
                MigrateUserTokens(originalOldUserId, newUserId);

                modified = true;
            }

            if (modified)
            {
                SaveStore(users);
                _logger.LogInformation("User registry migration completed successfully.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during users.json store migration.");
        }
        finally
        {
            _writeMutex.ReleaseMutex();
        }
    }

    private static bool IsSha256Hash(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length != 64)
        {
            return false;
        }

        return input.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
    }

    private void MigrateUserDirectory(string oldUserId, string newUserId)
    {
        try
        {
            string oldDir = Path.Combine(_dataFolder, oldUserId);
            string newDir = Path.Combine(_dataFolder, newUserId);

            if (Directory.Exists(oldDir))
            {
                if (!Directory.Exists(newDir))
                {
                    _logger.LogInformation("Moving user directory from {OldDir} to {NewDir}", oldDir, newDir);
                    Directory.Move(oldDir, newDir);
                }
                else
                {
                    _logger.LogInformation("Merging files from user directory {OldDir} to {NewDir}", oldDir, newDir);
                    foreach (string filePath in Directory.GetFiles(oldDir))
                    {
                        string fileName = Path.GetFileName(filePath);
                        string destPath = Path.Combine(newDir, fileName);
                        if (!File.Exists(destPath))
                        {
                            File.Move(filePath, destPath);
                        }
                        else
                        {
                            File.Delete(filePath);
                        }
                    }
                    Directory.Delete(oldDir, recursive: true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate user directory for old user {OldUserId}", oldUserId);
        }
    }

    private void MigrateUserTokens(string oldUserId, string newUserId)
    {
        string tokensPath = Path.Combine(_dataFolder, "apitokens.json");
        if (!File.Exists(tokensPath))
        {
            return;
        }

        string mutexName = "Global\\RfcBuddyTokens_" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(tokensPath))));
        using var tokenMutex = new System.Threading.Mutex(false, mutexName);

        tokenMutex.WaitOne();
        try
        {
            string json = File.ReadAllText(tokensPath);
            var tokens = JsonSerializer.Deserialize<List<ApiToken>>(json);
            if (tokens is null)
            {
                return;
            }

            bool modified = false;
            foreach (var token in tokens)
            {
                if (string.Equals(token.OwnerUserId, oldUserId, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Migrating API token {TokenId} owner: {OldUserId} -> {NewUserId}", token.Id, oldUserId, newUserId);
                    token.OwnerUserId = newUserId;
                    modified = true;
                }
            }

            if (modified)
            {
                string updatedJson = JsonSerializer.Serialize(tokens, new JsonSerializerOptions { WriteIndented = true });
                string tempPath = Path.Combine(_dataFolder, $"apitokens.json.tmp-{Guid.NewGuid():N}");
                File.WriteAllText(tempPath, updatedJson);
                File.Move(tempPath, tokensPath, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate API tokens for old user {OldUserId}", oldUserId);
        }
        finally
        {
            tokenMutex.ReleaseMutex();
        }
    }
}
