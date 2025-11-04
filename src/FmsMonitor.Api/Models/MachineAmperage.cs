namespace FmsMonitor.Api.Models;

/// <summary>
/// Thông tin Ampe của máy
/// </summary>
public class MachineAmperage
{
    public int Id { get; set; }
    public string MachineCode { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public decimal AmperageValue { get; set; }
    public DateTime RecordedAt { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Cấu hình giám sát cho máy
/// </summary>
public class MonitoringConfiguration
{
    public int Id { get; set; }
    public string MachineCode { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;

    // Ngưỡng
    public decimal MinAmperage { get; set; }
    public decimal MaxAmperage { get; set; }
    public decimal FluctuationMinAmperage { get; set; }
    public decimal FluctuationMaxAmperage { get; set; }

    // Điều kiện cảnh báo
    public int MonitoringWindowMinutes { get; set; } = 5; // Cửa sổ thời gian giám sát (phút)
    public int AllowedFluctuationCount { get; set; } = 1; // Số lần dao động được phép

    // Thời gian lấy dữ liệu
    public int DataFetchIntervalSeconds { get; set; } = 60; // Lấy dữ liệu mỗi 60 giây

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Cảnh báo
/// </summary>
public class Alert
{
    public int Id { get; set; }
    public string MachineCode { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public AlertLevel Level { get; set; }
    public AlertType Type { get; set; }
    public decimal CurrentValue { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? AiAnalysis { get; set; }
    public DateTime DetectedAt { get; set; }
    public bool IsNotified { get; set; }
    public DateTime? NotifiedAt { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
}

public enum AlertLevel
{
    Info = 0,
    Warning = 1,
    Critical = 2,
    Emergency = 3
}

public enum AlertType
{
    BelowMinimum = 1,
    AboveMaximum = 2,
    AbnormalFluctuation = 3,
    NoData = 4
}

/// <summary>
/// Người nhận email cảnh báo
/// </summary>
public class EmailRecipient
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // Nhân viên trực tiếp, Trưởng ca, Quản lý
    public AlertLevel MinAlertLevel { get; set; } = AlertLevel.Warning;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Kết quả phân tích dao động
/// </summary>
public class FluctuationAnalysis
{
    public string MachineCode { get; set; } = string.Empty;
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
    public int FluctuationCount { get; set; }
    public List<decimal> Values { get; set; } = new();
    public bool IsAbnormal { get; set; }
    public string? Reason { get; set; }
}
