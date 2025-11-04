namespace FmsMonitor.Api.Models;

/// <summary>
/// Request cho AI Agent phân tích
/// </summary>
public class AgentAnalysisRequest
{
    public string MachineCode { get; set; } = string.Empty;
    public List<MachineAmperage> RecentData { get; set; } = new();
    public MonitoringConfiguration Configuration { get; set; } = null!;
    public FluctuationAnalysis FluctuationAnalysis { get; set; } = null!;
}

/// <summary>
/// Response từ AI Agent
/// </summary>
public class AgentAnalysisResponse
{
    public bool IsAbnormal { get; set; }
    public string Analysis { get; set; } = string.Empty;
    public AlertLevel SuggestedAlertLevel { get; set; }
    public List<string> Recommendations { get; set; } = new();
    public string? PredictedIssue { get; set; }
}

/// <summary>
/// Ngữ cảnh cho AI Agent
/// </summary>
public class AgentContext
{
    public string MachineCode { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public List<Alert> RecentAlerts { get; set; } = new();
    public List<MachineAmperage> HistoricalData { get; set; } = new();
}
