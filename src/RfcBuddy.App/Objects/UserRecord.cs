namespace RfcBuddy.App.Objects;

public class UserRecord
{
    public string UserId { get; set; } = string.Empty;

    public string Identity { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public bool IsAdmin { get; set; }

    public DateTime FirstSeenUtc { get; set; } = DateTime.UtcNow;
}
