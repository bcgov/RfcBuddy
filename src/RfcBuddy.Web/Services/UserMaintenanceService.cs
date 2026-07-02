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
                    string userFolder = Path.Combine(dataFolder, userId);
                    if (Directory.Exists(userFolder))
                    {
                        Directory.Delete(userFolder, recursive: true);
                    }

                    tokenService.PurgeTokensForUser(userId);
                    registryService.RemoveUser(userId);
                    _logger.LogInformation("Removed inactive user data for {UserId}", userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to remove inactive user data for {UserId}", userId);
                }
            }
        }
    }
}
