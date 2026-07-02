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
    private static readonly ConcurrentDictionary<string, object> LockRegistry = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncRoot;
    private readonly string _dataFolder;
    private readonly string _storePath;
    private readonly ILogger<UserRegistryService> _logger;

    public UserRegistryService(string dataFolder, ILogger<UserRegistryService> logger)
    {
        _dataFolder = Path.GetFullPath(dataFolder);
        _storePath = Path.Combine(_dataFolder, "users.json");
        _logger = logger;
        _syncRoot = LockRegistry.GetOrAdd(_storePath, static _ => new object());
        Directory.CreateDirectory(_dataFolder);
    }

    public UserRecord EnsureRegistered(string userId, string identity, string email = "")
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        lock (_syncRoot)
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
    }

    public bool IsAdmin(string userId)
    {
        lock (_syncRoot)
        {
            return LoadStore().Any(x => string.Equals(x.UserId, userId, StringComparison.OrdinalIgnoreCase) && x.IsAdmin);
        }
    }

    public IReadOnlyList<UserListEntry> GetAllUsers()
    {
        lock (_syncRoot)
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
    }

    public bool SetAdmin(string targetUserId, bool isAdmin, string requestingAdminUserId)
    {
        lock (_syncRoot)
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
    }

    public void RemoveUser(string userId)
    {
        lock (_syncRoot)
        {
            List<UserRecord> users = LoadStore();
            users.RemoveAll(x => string.Equals(x.UserId, userId, StringComparison.OrdinalIgnoreCase));
            SaveStore(users);
        }
    }

    public IReadOnlyList<string> GetInactiveUserIds(TimeSpan threshold)
    {
        lock (_syncRoot)
        {
            List<UserRecord> users = LoadStore();
            DateTime cutoff = DateTime.UtcNow.Subtract(threshold);
            return [.. users.Where(x => GetLastActiveUtc(x.UserId) <= cutoff).Select(x => x.UserId)];
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
}
