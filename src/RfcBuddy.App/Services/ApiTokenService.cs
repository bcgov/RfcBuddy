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
    private static readonly ConcurrentDictionary<string, object> LockRegistry = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncRoot;
    private readonly string _dataFolder;
    private readonly string _storePath;
    private readonly ILogger<ApiTokenService> _logger;

    public ApiTokenService(string dataFolder, ILogger<ApiTokenService> logger)
    {
        _dataFolder = Path.GetFullPath(dataFolder);
        _storePath = Path.Combine(_dataFolder, "apitokens.json");
        _logger = logger;
        _syncRoot = LockRegistry.GetOrAdd(_storePath, static _ => new object());
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

        lock (_syncRoot)
        {
            List<ApiToken> tokens = LoadStore();
            var token = new ApiToken
            {
                Id = Guid.NewGuid().ToString("N"),
                OwnerUserId = userId,
                Label = label.Trim(),
                CreatedUtc = nowUtc,
                ExpiresUtc = effectiveExpiryUtc,
                Hash = tokenHash
            };
            tokens.Add(token);
            SaveStore(tokens);
            return new TokenCreationResult { Token = token, RawToken = rawToken };
        }
    }

    public IReadOnlyList<ApiToken> GetTokensForUser(string userId)
    {
        lock (_syncRoot)
        {
            return [.. LoadStore().Where(x => string.Equals(x.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))];
        }
    }

    public IReadOnlyList<ApiToken> GetAllTokens()
    {
        lock (_syncRoot)
        {
            return [.. LoadStore()];
        }
    }

    public AuthenticatedToken? Authenticate(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        string tokenHash = Cryptography.GetSha256Hash(rawToken);
        lock (_syncRoot)
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
    }

    public bool RevokeToken(string tokenId, string requestingUserId)
    {
        lock (_syncRoot)
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
    }

    public bool RevokeTokenAsAdmin(string tokenId)
    {
        lock (_syncRoot)
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
    }

    public void PurgeTokensForUser(string userId)
    {
        lock (_syncRoot)
        {
            List<ApiToken> tokens = LoadStore();
            List<ApiToken> remaining = tokens.Where(x => !string.Equals(x.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase)).ToList();
            SaveStore(remaining);
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
        catch (JsonException)
        {
            _logger.LogWarning("Token store was corrupted. Resetting it.");
            return [];
        }
    }

    private void SaveStore(IEnumerable<ApiToken> tokens)
    {
        string json = JsonSerializer.Serialize(tokens, new JsonSerializerOptions { WriteIndented = true });
        string tempPath = Path.Combine(_dataFolder, $"apitokens.json.tmp-{Guid.NewGuid():N}");
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _storePath, true);
    }
}
