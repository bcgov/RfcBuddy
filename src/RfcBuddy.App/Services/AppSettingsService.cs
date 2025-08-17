using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RfcBuddy.App.Objects;

namespace RfcBuddy.App.Services;

/// <summary>
/// Service to retrieve global application settings
/// </summary>
public interface IAppSettingsService
{
    /// <summary>
    /// The application settings
    /// </summary>
    public AppSettings AppSettings { get; }
}

/// <summary>
/// Service that reads the application settings from an appSettings.json file, including any environment-specific overrides
/// </summary>
public class AppSettingsService : IAppSettingsService
{
    /// <summary>
    /// appSettings.json key for the data folder
    /// </summary>
    private const string keyDataFolder = "DataFolder";

    /// <summary>
    /// appSettings.json section name for the following keys. The whole section should look like this:
    ///   "SourceInfo": {
    ///     "SourceUrl365": "...",
    ///     "SourceUrlUser": "...",
    ///     "SourceUrlPassword": "...",
    ///     "SourceRefreshMinutes": "..."
    ///   },
    /// </summary>
    private const string sectionSourceInfo = "SourceInfo";

    /// <summary>
    /// The URL of the Excel file with the 365-day changes
    /// </summary>
    private const string keySourceUrl365 = "SourceUrl365";

    /// <summary>
    /// The username to use when retrieving the file
    /// </summary>
    private const string keySourceUrlUser = "SourceUrlUser";

    /// <summary>
    /// The password to use when retrieving the file
    /// </summary>
    private const string keySourceUrlPassword = "SourceUrlPassword";

    /// <summary>
    /// The source file will only be retrieved every x minutes
    /// </summary>
    private const string keySourceRefreshMinutes = "SourceRefreshMinutes";

    private readonly AppSettings _appSettings;

    /// <summary>
    /// The AppSettings object that consumers of this service will reference
    /// </summary>
    AppSettings IAppSettingsService.AppSettings => _appSettings;

    /// <summary>
    /// Initializes a new AppSettingsService instance
    /// </summary>
    /// <param name="logger">Logger service</param>
    /// <param name="config">Configuration service</param>
    public AppSettingsService(ILogger<AppSettingsService> logger, IConfiguration config)
    {
        _appSettings = new()
        {
            DataFolder = config[keyDataFolder] ?? "./data",
        };
        if (string.IsNullOrWhiteSpace(config[sectionSourceInfo + ":" + keySourceUrl365]))
        {
            logger.LogCritical("Source URL for the 365-day change schedule not set in the appSettings.json file. This should be in under " + sectionSourceInfo + "/" + keySourceUrl365 + ".");
        }
        else
        {
            _appSettings.SourceUrl = config[sectionSourceInfo + ":" + keySourceUrl365]!;
            _appSettings.SourceUser = config[sectionSourceInfo + ":" + keySourceUrlUser] ?? string.Empty;
            _appSettings.SourcePassword = config[sectionSourceInfo + ":" + keySourceUrlPassword] ?? string.Empty;
        }
        if (int.TryParse(config[sectionSourceInfo + ":" + keySourceRefreshMinutes], out var refreshMinutes))
        {
            _appSettings.SourceRefreshInterval = refreshMinutes;
        }
        else
        {
            logger.LogError("Refresh interval minutes not set in appSettings.json file or not an integer. This should be in under " + sectionSourceInfo + "/" + keySourceRefreshMinutes + ".");
        }
    }

}
