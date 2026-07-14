namespace RfcBuddy.App.Objects;

public class ApiToken
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string OwnerUserId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresUtc { get; set; }

    public DateTime? LastUsedUtc { get; set; }

    public bool Revoked { get; set; }

    public string Hash { get; set; } = string.Empty;
}
