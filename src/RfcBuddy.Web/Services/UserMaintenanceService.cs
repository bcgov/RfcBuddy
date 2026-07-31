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
            try
            {
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            using IServiceScope scope = _scopeFactory.CreateScope();
            var appSettingsService = scope.ServiceProvider.GetRequiredService<IAppSettingsService>();
            var tokenService = scope.ServiceProvider.GetRequiredService<IApiTokenService>();
            var registryService = scope.ServiceProvider.GetRequiredService<IUserRegistryService>();
            string dataFolder = appSettingsService.AppSettings.DataFolder;

            foreach (string userId in registryService.GetInactiveUserIds(TimeSpan.FromDays(400)))
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                CleanupInactiveUser(userId, dataFolder, tokenService, registryService, stoppingToken);
            }
        }
    }

    private void CleanupInactiveUser(
        string userId,
        string dataFolder,
        IApiTokenService tokenService,
        IUserRegistryService registryService,
        CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            if (string.IsNullOrEmpty(userId) || Path.IsPathRooted(userId))
            {
                _logger.LogWarning("Skipping cleanup of user data. Invalid or rooted user ID: {UserId}", userId);
                return;
            }

            string baseFolder = Path.GetFullPath(dataFolder);
            string safeUserId = userId.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (Path.IsPathRooted(safeUserId))
            {
                _logger.LogWarning("Skipping cleanup of user data. Sanitized user ID is rooted: {UserId}", userId);
                return;
            }

            string userFolder = Path.GetFullPath(Path.Join(baseFolder, safeUserId));
            string normalizedBase = baseFolder.EndsWith(Path.DirectorySeparatorChar) ? baseFolder : baseFolder + Path.DirectorySeparatorChar;

            if (!userFolder.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Skipping cleanup of user data. User folder {UserFolder} is outside data folder {DataFolder}", userFolder, dataFolder);
                return;
            }

            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            if (Directory.Exists(userFolder))
            {
                Directory.Delete(userFolder, recursive: true);
            }

            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            tokenService.PurgeTokensForUser(userId);
            registryService.RemoveUser(userId);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Removed inactive user data for {UserId}", userId);
            }
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
