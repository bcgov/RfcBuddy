using Microsoft.Extensions.Hosting;
using RfcBuddy.App.Objects;
using RfcBuddy.App.Services;

namespace RfcBuddy.Web.Services;

public sealed class ArchiveUpdateService(IServiceScopeFactory scopeFactory, ILogger<ArchiveUpdateService> logger, IAppSettingsService appSettingsService) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<ArchiveUpdateService> _logger = logger;
    private readonly IAppSettingsService _appSettingsService = appSettingsService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await UpdateOnce(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromDays(Math.Max(1, _appSettingsService.AppSettings.ArchiveUpdateIntervalDays)), stoppingToken).ConfigureAwait(false);
                await UpdateOnce(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Archive background update failed.");
            }
        }
    }

    private async Task UpdateOnce(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        IRfcService rfcService = scope.ServiceProvider.GetRequiredService<IRfcService>();
        IRfcArchiveService archiveService = scope.ServiceProvider.GetRequiredService<IRfcArchiveService>();
        await rfcService.GetLatestChanges().ConfigureAwait(false);
        List<Rfc> allRfcs = rfcService.GetAllRfcs();
        archiveService.UpdateArchive(allRfcs.AsEnumerable());
        _logger.LogInformation("Background archive refresh completed. RfcCount={RfcCount}", allRfcs.Count);
    }
}
