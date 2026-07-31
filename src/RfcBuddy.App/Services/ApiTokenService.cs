using Microsoft.Extensions.Logging;
using RfcBuddy.App.Core;
using RfcBuddy.App.Objects;
using System.Collections.Concurrent;
using System.Text.Json;

namespace RfcBuddy.App.Services;

public interface IApiTokenService
{
    TokenCreationResult CreateToken(string userId, string label, DateTime requestedExpiryUtc);
    IReadOnlyList<ApiToken> GetTokensForUser(string userId);
    IReadOnlyList<ApiToken> GetAllTokens();
    AuthenticatedToken? Authenticate(string rawToken);
    bool RevokeToken(string tokenId, string requestingUserId);
    bool RevokeTokenAsAdmin(string tokenId);
    void PurgeTokensForUser(string userId);
    int MigrateUserTokens(string oldUserId, string newUserId);
}

public sealed class TokenCreationResult
{
    public required ApiToken Token { get; init; }
    public required string RawToken { get; init; }
}

public sealed class AuthenticatedToken
{
    public required string OwnerUserId { get; init; }
    public required string TokenId { get; init; }
}

public sealed class ApiTokenService : IApiTokenService
{
    private static readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
    private static readonly ConcurrentDictionary<string, System.Threading.Mutex> lockRegistry = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Threading.Mutex _writeMutex;
    private readonly string _dataFolder;
    private readonly string _storePath;
    private readonly ILogger<ApiTokenService> _logger;

    public ApiTokenService(string dataFolder, ILogger<ApiTokenService> logger)
    {
        _dataFolder = Path.GetFullPath(dataFolder);
        _storePath = Path.Join(_dataFolder, "apitokens.json");
        _logger = logger;
        _writeMutex = lockRegistry.GetOrAdd(_storePath, static path => new System.Threading.Mutex(false, "Global\\RfcBuddyTokens_" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path)))));
        Directory.CreateDirectory(_dataFolder);
    }

    public TokenCreationResult CreateToken(string userId, string label, DateTime requestedExpiryUtc)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        DateTime nowUtc = DateTime.UtcNow;
        DateTime maxExpiryUtc = nowUtc.AddDays(90);
        DateTime effectiveExpiryUtc = requestedExpiryUtc.ToUniversalTime();
        if (effectiveExpiryUtc > maxExpiryUtc)
        {
            effectiveExpiryUtc = maxExpiryUtc;
        }

        if (effectiveExpiryUtc <= nowUtc)
        {
            effectiveExpiryUtc = nowUtc.AddMinutes(1);
        }

        string rawToken = Guid.NewGuid().ToString("N") + ":" + Guid.NewGuid().ToString("N");
        string tokenHash = Cryptography.GetSha256Hash(rawToken);

        _writeMutex.WaitOne();
        try
        {
            List<ApiToken> tokens = LoadStore();
            var token = new ApiToken
            {
                Id = Guid.NewGuid().ToString("N"),
                OwnerUserId = userId,
                Label = new string(label.Trim().Where(c => !char.IsControl(c)).Take(100).ToArray()),

                CreatedUtc = nowUtc,
                ExpiresUtc = effectiveExpiryUtc,
                Hash = tokenHash
            };
            tokens.Add(token);
            SaveStore(tokens);
            return new TokenCreationResult { Token = token, RawToken = rawToken };
        }
        finally
        {
            _writeMutex.ReleaseMutex();
        }
    }

    public IReadOnlyList<ApiToken> GetTokensForUser(string userId)
    {
        _writeMutex.WaitOne();
        try
        {
            return [.. LoadStore().Where(x => string.Equals(x.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))];
        }
        finally
        {
            _writeMutex.ReleaseMutex();
        }
    }

    public IReadOnlyList<ApiToken> GetAllTokens()
    {
        _writeMutex.WaitOne();
        try
        {
            return [.. LoadStore()];
        }
        finally
        {
            _writeMutex.ReleaseMutex();
        }
    }

    public AuthenticatedToken? Authenticate(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        string tokenHash = Cryptography.GetSha256Hash(rawToken);
        _writeMutex.WaitOne();
        try
        {
            List<ApiToken> tokens = LoadStore();
            ApiToken? token = tokens.FirstOrDefault(x => string.Equals(x.Hash, tokenHash, StringComparison.OrdinalIgnoreCase));
            if (token is null || token.Revoked || token.ExpiresUtc <= DateTime.UtcNow)
            {
                return null;
            }

            token.LastUsedUtc = DateTime.UtcNow;
            SaveStore(tokens);
            return new AuthenticatedToken { OwnerUserId = token.OwnerUserId, TokenId = token.Id };
        }
        finally
        {
            _writeMutex.ReleaseMutex();
        }
    }

    public bool RevokeToken(string tokenId, string requestingUserId)
    {
        _writeMutex.WaitOne();
        try
        {
            List<ApiToken> tokens = LoadStore();
            ApiToken? token = tokens.FirstOrDefault(x => string.Equals(x.Id, tokenId, StringComparison.OrdinalIgnoreCase));
            if (token is null || !string.Equals(token.OwnerUserId, requestingUserId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            token.Revoked = true;
            SaveStore(tokens);
            return true;
        }
        finally
        {
            _writeMutex.ReleaseMutex();
        }
    }

    public bool RevokeTokenAsAdmin(string tokenId)
    {
        _writeMutex.WaitOne();
        try
        {
            List<ApiToken> tokens = LoadStore();
            ApiToken? token = tokens.FirstOrDefault(x => string.Equals(x.Id, tokenId, StringComparison.OrdinalIgnoreCase));
            if (token is null)
            {
                return false;
            }

            token.Revoked = true;
            SaveStore(tokens);
            return true;
        }
        finally
        {
            _writeMutex.ReleaseMutex();
        }
    }

    public void PurgeTokensForUser(string userId)
    {
        _writeMutex.WaitOne();
        try
        {
            List<ApiToken> tokens = LoadStore();
            List<ApiToken> remaining = tokens.Where(x => !string.Equals(x.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase)).ToList();
            SaveStore(remaining);
        }
        finally
        {
            _writeMutex.ReleaseMutex();
        }
    }

    // REMINDER: REMOVE MIGRATION CODE BY 2027-09-03 (400 days from 2026-07-30).
    // By this date, all users will either have logged in and been migrated, or cleaned up after 400 days of inactivity.
    public int MigrateUserTokens(string oldUserId, string newUserId)
    {
        if (string.IsNullOrWhiteSpace(oldUserId) || string.IsNullOrWhiteSpace(newUserId) || string.Equals(oldUserId, newUserId, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        _writeMutex.WaitOne();
        try
        {
            List<ApiToken> tokens = LoadStore();
            int migratedCount = 0;
            foreach (ApiToken token in tokens.Where(x => string.Equals(x.OwnerUserId, oldUserId, StringComparison.OrdinalIgnoreCase)))
            {
                token.OwnerUserId = newUserId;
                migratedCount++;
            }

            if (migratedCount > 0)
            {
                SaveStore(tokens);
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Migrated {Count} API token(s) from user ID {OldUserId} to {NewUserId}", migratedCount, oldUserId, newUserId);
                }
            }

            return migratedCount;
        }
        finally
        {
            _writeMutex.ReleaseMutex();
        }
    }

    private List<ApiToken> LoadStore()
    {
        if (!File.Exists(_storePath))
        {
            return [];
        }

        try
        {
            string json = File.ReadAllText(_storePath);
            return JsonSerializer.Deserialize<List<ApiToken>>(json) ?? [];
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Token store was corrupted. Resetting it.");
            return [];
        }
    }

    private void SaveStore(IEnumerable<ApiToken> tokens)
    {
        string json = JsonSerializer.Serialize(tokens, jsonOptions);
        string tempPath = Path.Join(_dataFolder, $"apitokens.json.tmp-{Guid.NewGuid():N}");
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _storePath, true);
    }
}
