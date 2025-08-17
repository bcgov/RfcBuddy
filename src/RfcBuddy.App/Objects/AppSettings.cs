namespace RfcBuddy.App.Objects;

/// <summary>
/// Holds global application settings
/// </summary>
public class AppSettings
{
    /// <summary>
    /// The application's folder for persistent data.
    /// </summary>
    public string DataFolder { get; set; } = string.Empty;

    /// <summary>
    /// The URL to retrieve the 365-day change schedule Excel sheet.
    /// </summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>
    /// The username to retrieve the source URL.
    /// </summary>
    public string SourceUser { get; set; } = string.Empty;

    /// <summary>
    /// The password to retrieve the source URL.
    /// </summary>
    public string SourcePassword { get; set; } = string.Empty;

    /// <summary>
    /// The source URL refresh interval in minutes. Defaults to 60.
    /// </summary>
    public int SourceRefreshInterval { get; set; } = 60;
}
