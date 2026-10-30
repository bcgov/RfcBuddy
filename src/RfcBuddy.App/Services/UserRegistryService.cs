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
    bool MarkUserCleanupPending(string userId);
    bool RemoveUser(string userId);
    IReadOnlyList<string> GetInactiveUserIds(TimeSpan threshold);
    bool MigrateUserHash(string oldUserId, string newUserId);
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
    private static readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
    private static readonly ConcurrentDictionary<string, System.Threading.Mutex> lockRegistry = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Threading.Mutex _writeMutex;
    private readonly string _dataFolder;
    private readonly string _storePath;
    private readonly ILogger<UserRegistryService> _logger;
    private readonly IApiTokenService? _apiTokenService;

    public UserRegistryService(string dataFolder, ILogger<UserRegistryService> logger, IApiTokenService? apiTokenService = null)
    {
        _dataFolder = Path.GetFullPath(dataFolder);
        _storePath = Path.Join(_dataFolder, "users.json");
        _logger = logger;
        _apiTokenService = apiTokenService;
        _writeMutex = lockRegistry.GetOrAdd(_storePath, static path => new System.Threading.Mutex(false, "Global\\RfcBuddyUsers_" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path)))));
        Directory.CreateDirectory(_dataFolder);
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
            if (string.IsNullOrWhiteSpace(requestingAdminUserId)
                || !users.Any(x => x.IsAdmin && string.Equals(x.UserId, requestingAdminUserId, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            UserRecord? target = users.FirstOrDefault(x => string.Equals(x.UserId, targetUserId, StringComparison.OrdinalIgnoreCase));
            if (target is null)
            {
                return false;
            }

            if (!isAdmin && !users.Any(x => x.IsAdmin && !string.Equals(x.UserId, targetUserId, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            if (isAdmin && target.CleanupPending)
            {
                return false;
            }

            if (!isAdmin && users.Any(x => x.IsAdmin && x.CleanupPending))
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

    public bool MarkUserCleanupPending(string userId)
    {
        _writeMutex.WaitOne();
        try
        {
            List<UserRecord> users = LoadStore();
            UserRecord? target = users.FirstOrDefault(x => string.Equals(x.UserId, userId, StringComparison.OrdinalIgnoreCase));
            if (target is null)
            {
                return false;
            }

            if (target.CleanupPending)
            {
                return true;
            }

            if (target.IsAdmin
                && (users.Count(x => x.IsAdmin) <= 1
                    || users.Any(x => x.IsAdmin && x.CleanupPending)))
            {
                return false;
            }

            target.CleanupPending = true;
            SaveStore(users);
            return true;
        }
        finally
        {
            _writeMutex.ReleaseMutex();
        }
    }

    public bool RemoveUser(string userId)
    {
        _writeMutex.WaitOne();
        try
        {
            List<UserRecord> users = LoadStore();
            UserRecord? target = users.FirstOrDefault(x => string.Equals(x.UserId, userId, StringComparison.OrdinalIgnoreCase));
            if (target is null)
            {
                return true;
            }

            if (target.IsAdmin
                && (users.Any(x => x.IsAdmin && x.CleanupPending && !ReferenceEquals(x, target))
                    || !users.Any(x => x.IsAdmin && !ReferenceEquals(x, target))))
            {
                return false;
            }

            users.Remove(target);
            SaveStore(users);
            return true;
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
            // Oldest first, so that when every admin is inactive the most recently active one is retained.
            return [.. users
                .Select(x =>
                {
                    DateTime lastActiveUtc = GetLastActiveUtc(x.UserId);
                    return new
                    {
                        x.UserId,
                        x.CleanupPending,
                        EffectiveLastActiveUtc = lastActiveUtc == DateTime.MinValue ? x.FirstSeenUtc : lastActiveUtc
                    };
                })
                .Where(x => x.CleanupPending || x.EffectiveLastActiveUtc <= cutoff)
                .OrderBy(x => x.EffectiveLastActiveUtc)
                .Select(x => x.UserId)];
        }
        finally
        {
            _writeMutex.ReleaseMutex();
        }
    }

    // REMINDER: REMOVE MIGRATION CODE BY 2027-09-03 (400 days from 2026-07-30).
    public bool MigrateUserHash(string oldUserId, string newUserId)
    {
        if (string.IsNullOrWhiteSpace(oldUserId) || string.IsNullOrWhiteSpace(newUserId) || string.Equals(oldUserId, newUserId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _writeMutex.WaitOne();
        try
        {
            List<UserRecord> users = LoadStore();
            UserRecord? target = users.FirstOrDefault(x => string.Equals(x.UserId, oldUserId, StringComparison.OrdinalIgnoreCase));
            if (target is null)
            {
                return false;
            }

            target.UserId = newUserId;
            SaveStore(users);

            MigrateUserFolder(oldUserId, newUserId);
            _apiTokenService?.MigrateUserTokens(oldUserId, newUserId);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Migrated user record and data folder from {OldUserId} to {NewUserId}", oldUserId, newUserId);
            }
            return true;
        }
        finally
        {
            _writeMutex.ReleaseMutex();
        }
    }

    private void MigrateUserFolder(string oldUserId, string newUserId)
    {
        if (string.IsNullOrWhiteSpace(oldUserId) || string.IsNullOrWhiteSpace(newUserId) || string.Equals(oldUserId, newUserId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string oldSanitized = Path.GetFileName(oldUserId);
        string newSanitized = Path.GetFileName(newUserId);
        if (string.IsNullOrEmpty(oldSanitized) || string.IsNullOrEmpty(newSanitized) || Path.IsPathRooted(oldSanitized) || Path.IsPathRooted(newSanitized))
        {
            return;
        }

        string oldFolderPath = Path.GetFullPath(Path.Join(_dataFolder, oldSanitized));
        string newFolderPath = Path.GetFullPath(Path.Join(_dataFolder, newSanitized));

        string normalizedBase = _dataFolder.EndsWith(Path.DirectorySeparatorChar) ? _dataFolder : _dataFolder + Path.DirectorySeparatorChar;
        if (!oldFolderPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase) || !newFolderPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Skipping user folder migration outside base data directory.");
            return;
        }

        if (Directory.Exists(oldFolderPath))
        {
            if (!Directory.Exists(newFolderPath))
            {
                Directory.Move(oldFolderPath, newFolderPath);
            }
            else
            {
                foreach (string file in Directory.GetFiles(oldFolderPath))
                {
                    string fileName = Path.GetFileName(file);
                    string destFile = Path.Join(newFolderPath, fileName);
                    if (!File.Exists(destFile))
                    {
                        File.Move(file, destFile);
                    }
                }
                Directory.Delete(oldFolderPath, recursive: true);
            }
        }
    }

    private DateTime GetLastActiveUtc(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return DateTime.MinValue;
        }

        string sanitizedUserId = Path.GetFileName(userId);
        if (sanitizedUserId != userId || string.IsNullOrEmpty(sanitizedUserId) || Path.IsPathRooted(sanitizedUserId))
        {
            return DateTime.MinValue;
        }

        string userFolder = Path.GetFullPath(Path.Join(_dataFolder, sanitizedUserId));
        string normalizedBase = _dataFolder.EndsWith(Path.DirectorySeparatorChar) ? _dataFolder : _dataFolder + Path.DirectorySeparatorChar;
        if (!userFolder.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
        {
            return DateTime.MinValue;
        }

        string[] filePaths = [Path.Join(userFolder, "PreviousRFCs.txt"), Path.Join(userFolder, "ApiPreviousRFCs.txt")];
        return filePaths
            .Where(File.Exists)
            .Select(File.GetLastWriteTimeUtc)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();
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
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "User store was corrupted. Resetting it.");
            return [];
        }
    }

    private void SaveStore(IEnumerable<UserRecord> users)
    {
        string json = JsonSerializer.Serialize(users, jsonOptions);
        string tempPath = Path.Join(_dataFolder, $"users.json.tmp-{Guid.NewGuid():N}");
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _storePath, true);
    }
}
