using FmsMonitor.Api.Models;
using FmsMonitor.Api.Repositories;

namespace FmsMonitor.Api.Services;

/// <summary>
/// Service giám sát giá trị Ampe và phát hiện bất thường
/// </summary>
public interface IAmperageMonitoringService
{
    Task<FluctuationAnalysis> AnalyzeFluctuationAsync(string machineCode, MonitoringConfiguration config);
    Task<bool> CheckThresholdsAsync(decimal value, MonitoringConfiguration config);
    Task MonitorAllMachinesAsync();
}

public class AmperageMonitoringService : IAmperageMonitoringService
{
    private readonly IMachineAmperageRepository _amperageRepository;
    private readonly IMonitoringConfigurationRepository _configRepository;
    private readonly IAlertRepository _alertRepository;
    private readonly IAnomalyDetectionAgent _anomalyAgent;
    private readonly IEmailNotificationService _emailService;
    private readonly ILogger<AmperageMonitoringService> _logger;

    public AmperageMonitoringService(
        IMachineAmperageRepository amperageRepository,
        IMonitoringConfigurationRepository configRepository,
        IAlertRepository alertRepository,
        IAnomalyDetectionAgent anomalyAgent,
        IEmailNotificationService emailService,
        ILogger<AmperageMonitoringService> logger)
    {
        _amperageRepository = amperageRepository;
        _configRepository = configRepository;
        _alertRepository = alertRepository;
        _anomalyAgent = anomalyAgent;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<FluctuationAnalysis> AnalyzeFluctuationAsync(string machineCode, MonitoringConfiguration config)
    {
        var windowStart = DateTime.UtcNow.AddMinutes(-config.MonitoringWindowMinutes);
        var windowEnd = DateTime.UtcNow;

        var recentData = await _amperageRepository.GetRecentDataAsync(machineCode, config.MonitoringWindowMinutes);
        var dataList = recentData.ToList();

        var analysis = new FluctuationAnalysis
        {
            MachineCode = machineCode,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            Values = dataList.Select(d => d.AmperageValue).ToList()
        };

        // Đếm số lần dao động ngoài khoảng cho phép
        int fluctuationCount = 0;
        foreach (var value in analysis.Values)
        {
            if (value < config.FluctuationMinAmperage || value > config.FluctuationMaxAmperage)
            {
                fluctuationCount++;
            }
        }

        analysis.FluctuationCount = fluctuationCount;

        // Kiểm tra bất thường
        if (fluctuationCount > config.AllowedFluctuationCount)
        {
            analysis.IsAbnormal = true;
            analysis.Reason = $"Phát hiện {fluctuationCount} dao động vượt ngưỡng trong {config.MonitoringWindowMinutes} phút " +
                            $"(cho phép tối đa {config.AllowedFluctuationCount} dao động). " +
                            $"Khoảng dao động bình thường: {config.FluctuationMinAmperage}A - {config.FluctuationMaxAmperage}A";
        }
        else
        {
            analysis.IsAbnormal = false;
            analysis.Reason = $"Dao động trong giới hạn cho phép ({fluctuationCount}/{config.AllowedFluctuationCount})";
        }

        return analysis;
    }

    public async Task<bool> CheckThresholdsAsync(decimal value, MonitoringConfiguration config)
    {
        // Kiểm tra ngưỡng MIN, MAX
        if (value < config.MinAmperage)
        {
            _logger.LogWarning("Máy {MachineCode}: Giá trị {Value}A thấp hơn ngưỡng MIN {Min}A",
                config.MachineCode, value, config.MinAmperage);
            return false;
        }

        if (value > config.MaxAmperage)
        {
            _logger.LogWarning("Máy {MachineCode}: Giá trị {Value}A vượt ngưỡng MAX {Max}A",
                config.MachineCode, value, config.MaxAmperage);
            return false;
        }

        return true;
    }

    public async Task MonitorAllMachinesAsync()
    {
        try
        {
            _logger.LogInformation("Bắt đầu giám sát tất cả các máy");

            var configs = await _configRepository.GetAllActiveAsync();
            var configList = configs.ToList();

            foreach (var config in configList)
            {
                try
                {
                    await MonitorMachineAsync(config);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi giám sát máy {MachineCode}", config.MachineCode);
                }
            }

            _logger.LogInformation("Hoàn thành giám sát {Count} máy", configList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi trong quá trình giám sát");
            throw;
        }
    }

    private async Task MonitorMachineAsync(MonitoringConfiguration config)
    {
        _logger.LogDebug("Giám sát máy {MachineCode}", config.MachineCode);

        // Lấy dữ liệu mới nhất
        var latestData = await _amperageRepository.GetLatestAsync(config.MachineCode);
        if (latestData == null)
        {
            _logger.LogWarning("Không có dữ liệu cho máy {MachineCode}", config.MachineCode);
            await CreateAlertAsync(config, AlertType.NoData, 0, "Không nhận được dữ liệu từ máy");
            return;
        }

        // Kiểm tra ngưỡng MIN/MAX
        if (latestData.AmperageValue < config.MinAmperage)
        {
            await HandleThresholdViolationAsync(config, latestData, AlertType.BelowMinimum);
            return;
        }

        if (latestData.AmperageValue > config.MaxAmperage)
        {
            await HandleThresholdViolationAsync(config, latestData, AlertType.AboveMaximum);
            return;
        }

        // Phân tích dao động
        var fluctuationAnalysis = await AnalyzeFluctuationAsync(config.MachineCode, config);
        if (fluctuationAnalysis.IsAbnormal)
        {
            await HandleAbnormalFluctuationAsync(config, latestData, fluctuationAnalysis);
        }
    }

    private async Task HandleThresholdViolationAsync(
        MonitoringConfiguration config,
        MachineAmperage latestData,
        AlertType alertType)
    {
        _logger.LogWarning("Máy {MachineCode} vi phạm ngưỡng: {Type}, Giá trị: {Value}A",
            config.MachineCode, alertType, latestData.AmperageValue);

        // Lấy dữ liệu gần đây để AI phân tích
        var recentData = await _amperageRepository.GetRecentDataAsync(config.MachineCode, 30);
        var recentAlerts = await _alertRepository.GetRecentByMachineCodeAsync(config.MachineCode, 24);

        // Gọi AI Agent phân tích
        var analysisRequest = new AgentAnalysisRequest
        {
            MachineCode = config.MachineCode,
            RecentData = recentData.ToList(),
            Configuration = config,
            FluctuationAnalysis = new FluctuationAnalysis
            {
                MachineCode = config.MachineCode,
                IsAbnormal = true,
                Reason = $"Giá trị {latestData.AmperageValue}A vi phạm ngưỡng"
            }
        };

        var aiAnalysis = await _anomalyAgent.AnalyzeAsync(analysisRequest, new AgentContext
        {
            MachineCode = config.MachineCode,
            MachineName = config.MachineName,
            RecentAlerts = recentAlerts.ToList(),
            HistoricalData = recentData.ToList()
        });

        // Tạo alert
        var alert = new Alert
        {
            MachineCode = config.MachineCode,
            MachineName = config.MachineName,
            Level = aiAnalysis.SuggestedAlertLevel,
            Type = alertType,
            CurrentValue = latestData.AmperageValue,
            Message = $"Vi phạm ngưỡng: {GetAlertTypeMessage(alertType, latestData.AmperageValue, config)}",
            AiAnalysis = aiAnalysis.Analysis,
            DetectedAt = DateTime.UtcNow
        };

        await _alertRepository.InsertAsync(alert);

        // Gửi email thông báo
        await _emailService.SendAlertAsync(alert, aiAnalysis.Recommendations);
    }

    private async Task HandleAbnormalFluctuationAsync(
        MonitoringConfiguration config,
        MachineAmperage latestData,
        FluctuationAnalysis fluctuationAnalysis)
    {
        _logger.LogWarning("Máy {MachineCode} có dao động bất thường: {Reason}",
            config.MachineCode, fluctuationAnalysis.Reason);

        var recentData = await _amperageRepository.GetRecentDataAsync(config.MachineCode, 30);
        var recentAlerts = await _alertRepository.GetRecentByMachineCodeAsync(config.MachineCode, 24);

        var analysisRequest = new AgentAnalysisRequest
        {
            MachineCode = config.MachineCode,
            RecentData = recentData.ToList(),
            Configuration = config,
            FluctuationAnalysis = fluctuationAnalysis
        };

        var aiAnalysis = await _anomalyAgent.AnalyzeAsync(analysisRequest, new AgentContext
        {
            MachineCode = config.MachineCode,
            MachineName = config.MachineName,
            RecentAlerts = recentAlerts.ToList(),
            HistoricalData = recentData.ToList()
        });

        var alert = new Alert
        {
            MachineCode = config.MachineCode,
            MachineName = config.MachineName,
            Level = aiAnalysis.SuggestedAlertLevel,
            Type = AlertType.AbnormalFluctuation,
            CurrentValue = latestData.AmperageValue,
            Message = fluctuationAnalysis.Reason ?? "Dao động bất thường",
            AiAnalysis = aiAnalysis.Analysis,
            DetectedAt = DateTime.UtcNow
        };

        await _alertRepository.InsertAsync(alert);
        await _emailService.SendAlertAsync(alert, aiAnalysis.Recommendations);
    }

    private async Task CreateAlertAsync(MonitoringConfiguration config, AlertType type, decimal value, string message)
    {
        var alert = new Alert
        {
            MachineCode = config.MachineCode,
            MachineName = config.MachineName,
            Level = AlertLevel.Warning,
            Type = type,
            CurrentValue = value,
            Message = message,
            DetectedAt = DateTime.UtcNow
        };

        await _alertRepository.InsertAsync(alert);
        await _emailService.SendAlertAsync(alert, new List<string>());
    }

    private string GetAlertTypeMessage(AlertType type, decimal value, MonitoringConfiguration config)
    {
        return type switch
        {
            AlertType.BelowMinimum => $"Giá trị {value}A thấp hơn ngưỡng MIN {config.MinAmperage}A",
            AlertType.AboveMaximum => $"Giá trị {value}A vượt ngưỡng MAX {config.MaxAmperage}A",
            AlertType.NoData => "Không nhận được dữ liệu từ máy",
            _ => "Bất thường không xác định"
        };
    }
}
