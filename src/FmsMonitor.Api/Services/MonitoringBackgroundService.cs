namespace FmsMonitor.Api.Services;

/// <summary>
/// Background Service chạy giám sát liên tục
/// </summary>
public class MonitoringBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MonitoringBackgroundService> _logger;
    private readonly IConfiguration _configuration;

    public MonitoringBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<MonitoringBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Monitoring Background Service đang khởi động...");

        // Delay để đảm bảo application đã khởi động hoàn toàn
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunMonitoringCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong chu kỳ giám sát");
            }

            // Chờ trước khi chạy chu kỳ tiếp theo
            var intervalSeconds = _configuration.GetValue<int>("Monitoring:IntervalSeconds", 60);
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }

        _logger.LogInformation("Monitoring Background Service đã dừng");
    }

    private async Task RunMonitoringCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var monitoringService = scope.ServiceProvider.GetRequiredService<IAmperageMonitoringService>();
        var syncService = scope.ServiceProvider.GetRequiredService<IDataSynchronizationService>();

        try
        {
            // 1. Giám sát tất cả các máy
            _logger.LogDebug("Bắt đầu chu kỳ giám sát...");
            await monitoringService.MonitorAllMachinesAsync();

            // 2. Đồng bộ dữ liệu sang PostgreSQL (nếu được bật)
            var enableSync = _configuration.GetValue<bool>("PostgreSQL:EnableSync", false);
            if (enableSync)
            {
                _logger.LogDebug("Đồng bộ dữ liệu sang PostgreSQL...");
                var syncMinutes = _configuration.GetValue<int>("PostgreSQL:SyncIntervalMinutes", 60);
                await syncService.SynchronizeRecentDataAsync(syncMinutes);
            }

            _logger.LogDebug("Hoàn thành chu kỳ giám sát");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi trong chu kỳ giám sát");
        }
    }
}
