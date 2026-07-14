using RfcBuddy.App.Services;

namespace RfcBuddy.Web.Services;

public sealed class UserMaintenanceService(
    IServiceScopeFactory scopeFactory,
    ILogger<UserMaintenanceService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<UserMaintenanceService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken).ConfigureAwait(false);
            using IServiceScope scope = _scopeFactory.CreateScope();
            var appSettingsService = scope.ServiceProvider.GetRequiredService<IAppSettingsService>();
            var tokenService = scope.ServiceProvider.GetRequiredService<IApiTokenService>();
            var registryService = scope.ServiceProvider.GetRequiredService<IUserRegistryService>();
            string dataFolder = appSettingsService.AppSettings.DataFolder;
            foreach (string userId in registryService.GetInactiveUserIds(TimeSpan.FromDays(400)))
            {
                try
                {
                    if (string.IsNullOrEmpty(userId) || Path.IsPathRooted(userId))
                    {
                        _logger.LogWarning("Skipping cleanup of user data. Invalid or rooted user ID: {UserId}", userId);
                        continue;
                    }

                    string baseFolder = Path.GetFullPath(dataFolder);
                    string userFolder = Path.GetFullPath(Path.Combine(baseFolder, userId));
                    string normalizedBase = baseFolder.EndsWith(Path.DirectorySeparatorChar) ? baseFolder : baseFolder + Path.DirectorySeparatorChar;

                    if (!userFolder.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("Skipping cleanup of user data. User folder {UserFolder} is outside data folder {DataFolder}", userFolder, dataFolder);
                        continue;
                    }

                    if (Directory.Exists(userFolder))
                    {
                        Directory.Delete(userFolder, recursive: true);
                    }

                    tokenService.PurgeTokensForUser(userId);
                    registryService.RemoveUser(userId);

                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("Removed inactive user data for {UserId}", userId);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (IOException ex)
                {
                    _logger.LogError(ex, "Failed to remove inactive user data for {UserId}", userId);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogError(ex, "Failed to remove inactive user data for {UserId}", userId);
                }
            }
        }
    }
}
