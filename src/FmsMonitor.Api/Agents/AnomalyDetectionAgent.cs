using FmsMonitor.Api.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using System.Text;

namespace FmsMonitor.Api.Services;

/// <summary>
/// AI Agent phát hiện bất thường sử dụng Microsoft Semantic Kernel và Ollama
/// </summary>
public interface IAnomalyDetectionAgent
{
    Task<AgentAnalysisResponse> AnalyzeAsync(AgentAnalysisRequest request, AgentContext context);
}

public class AnomalyDetectionAgent : IAnomalyDetectionAgent
{
    private readonly Kernel _kernel;
    private readonly ILogger<AnomalyDetectionAgent> _logger;

    public AnomalyDetectionAgent(IConfiguration configuration, ILogger<AnomalyDetectionAgent> logger)
    {
        _logger = logger;

        // Khởi tạo Semantic Kernel với Ollama
        var ollamaEndpoint = configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
        var ollamaModel = configuration["Ollama:Model"] ?? "gpt-oss:120b-cloud";

        var builder = Kernel.CreateBuilder();

        builder.AddOllamaChatCompletion(
            modelId: ollamaModel,
            endpoint: new Uri(ollamaEndpoint));

        _kernel = builder.Build();
    }

    public async Task<AgentAnalysisResponse> AnalyzeAsync(AgentAnalysisRequest request, AgentContext context)
    {
        try
        {
            _logger.LogInformation("AI Agent đang phân tích máy {MachineCode}", request.MachineCode);

            // Chuẩn bị prompt cho AI
            var prompt = BuildAnalysisPrompt(request, context);

            // Gọi AI để phân tích
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(GetSystemPrompt());
            chatHistory.AddUserMessage(prompt);

            var response = await chatService.GetChatMessageContentAsync(chatHistory);
            var analysisText = response.Content ?? string.Empty;

            // Parse response và tạo kết quả
            var result = ParseAiResponse(analysisText, request);

            _logger.LogInformation("AI Agent hoàn thành phân tích máy {MachineCode}: {IsAbnormal}",
                request.MachineCode, result.IsAbnormal);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi AI Agent phân tích máy {MachineCode}", request.MachineCode);

            // Trả về kết quả mặc định khi có lỗi
            return new AgentAnalysisResponse
            {
                IsAbnormal = true,
                Analysis = "Không thể phân tích do lỗi AI Agent. Vui lòng kiểm tra thủ công.",
                SuggestedAlertLevel = AlertLevel.Warning,
                Recommendations = new List<string>
                {
                    "Kiểm tra kết nối AI Agent",
                    "Xem xét dữ liệu thủ công"
                }
            };
        }
    }

    private string GetSystemPrompt()
    {
        return @"Bạn là một chuyên gia AI trong lĩnh vực giám sát và bảo trì thiết bị sản xuất.
Nhiệm vụ của bạn là phân tích dữ liệu Ampe (dòng điện) từ các máy móc trong nhà máy sản xuất
và phát hiện các bất thường, dự đoán sự cố, đưa ra khuyến nghị.

Khi phân tích, hãy xem xét:
1. Xu hướng thay đổi của dòng điện
2. Tần suất dao động
3. So sánh với các lần cảnh báo trước
4. Mức độ nghiêm trọng của bất thường
5. Nguyên nhân có thể gây ra

Trả lời theo format JSON với các trường:
- isAbnormal (true/false): Có bất thường hay không
- analysis (string): Phân tích chi tiết
- suggestedAlertLevel (0-3): Mức cảnh báo đề xuất (0=Info, 1=Warning, 2=Critical, 3=Emergency)
- recommendations (array): Danh sách khuyến nghị
- predictedIssue (string, optional): Vấn đề dự đoán

Trả lời bằng tiếng Việt, ngắn gọn, súc tích, tập trung vào hành động cụ thể.";
    }

    private string BuildAnalysisPrompt(AgentAnalysisRequest request, AgentContext context)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# Thông tin máy");
        sb.AppendLine($"- Mã máy: {request.MachineCode}");
        sb.AppendLine($"- Tên máy: {request.Configuration.MachineName}");
        sb.AppendLine();

        sb.AppendLine($"# Cấu hình giám sát");
        sb.AppendLine($"- Ngưỡng MIN: {request.Configuration.MinAmperage}A");
        sb.AppendLine($"- Ngưỡng MAX: {request.Configuration.MaxAmperage}A");
        sb.AppendLine($"- Dao động bình thường: {request.Configuration.FluctuationMinAmperage}A - {request.Configuration.FluctuationMaxAmperage}A");
        sb.AppendLine($"- Cửa sổ giám sát: {request.Configuration.MonitoringWindowMinutes} phút");
        sb.AppendLine($"- Số dao động cho phép: {request.Configuration.AllowedFluctuationCount}");
        sb.AppendLine();

        sb.AppendLine($"# Phân tích dao động hiện tại");
        sb.AppendLine($"- Thời gian: {request.FluctuationAnalysis.WindowStart:yyyy-MM-dd HH:mm} - {request.FluctuationAnalysis.WindowEnd:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"- Số dao động phát hiện: {request.FluctuationAnalysis.FluctuationCount}");
        sb.AppendLine($"- Bất thường: {(request.FluctuationAnalysis.IsAbnormal ? "CÓ" : "KHÔNG")}");
        sb.AppendLine($"- Lý do: {request.FluctuationAnalysis.Reason}");
        sb.AppendLine();

        sb.AppendLine($"# Dữ liệu gần đây ({request.RecentData.Count} mẫu)");
        foreach (var data in request.RecentData.Take(10))
        {
            sb.AppendLine($"- {data.RecordedAt:HH:mm:ss}: {data.AmperageValue}A");
        }
        sb.AppendLine();

        if (context.RecentAlerts.Any())
        {
            sb.AppendLine($"# Cảnh báo gần đây ({context.RecentAlerts.Count})");
            foreach (var alert in context.RecentAlerts.Take(5))
            {
                sb.AppendLine($"- [{alert.DetectedAt:yyyy-MM-dd HH:mm}] {alert.Level}: {alert.Message}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("# Yêu cầu");
        sb.AppendLine("Dựa trên thông tin trên, hãy:");
        sb.AppendLine("1. Phân tích tình trạng hiện tại của máy");
        sb.AppendLine("2. Đánh giá mức độ nghiêm trọng");
        sb.AppendLine("3. Dự đoán vấn đề có thể xảy ra");
        sb.AppendLine("4. Đưa ra khuyến nghị cụ thể");

        return sb.ToString();
    }

    private AgentAnalysisResponse ParseAiResponse(string aiResponse, AgentAnalysisRequest request)
    {
        try
        {
            // Thử parse JSON response
            var jsonStart = aiResponse.IndexOf('{');
            var jsonEnd = aiResponse.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonText = aiResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var result = System.Text.Json.JsonSerializer.Deserialize<AgentAnalysisResponse>(jsonText,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (result != null)
                {
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể parse JSON từ AI response, sử dụng text analysis");
        }

        // Fallback: Phân tích text thông thường
        var response = new AgentAnalysisResponse
        {
            IsAbnormal = request.FluctuationAnalysis.IsAbnormal,
            Analysis = aiResponse,
            Recommendations = new List<string>()
        };

        // Xác định mức độ cảnh báo dựa trên số lần dao động
        var fluctuationRatio = (double)request.FluctuationAnalysis.FluctuationCount / request.Configuration.AllowedFluctuationCount;

        if (fluctuationRatio <= 1.0)
        {
            response.SuggestedAlertLevel = AlertLevel.Info;
        }
        else if (fluctuationRatio <= 2.0)
        {
            response.SuggestedAlertLevel = AlertLevel.Warning;
        }
        else if (fluctuationRatio <= 3.0)
        {
            response.SuggestedAlertLevel = AlertLevel.Critical;
        }
        else
        {
            response.SuggestedAlertLevel = AlertLevel.Emergency;
        }

        // Extract recommendations từ text (tìm các dòng bắt đầu bằng số hoặc dấu -)
        var lines = aiResponse.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("-") ||
                (trimmed.Length > 2 && char.IsDigit(trimmed[0]) && trimmed[1] == '.'))
            {
                var recommendation = trimmed.TrimStart('-', ' ', '\t', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', ' ');
                if (!string.IsNullOrWhiteSpace(recommendation))
                {
                    response.Recommendations.Add(recommendation);
                }
            }
        }

        // Nếu không tìm thấy recommendations, thêm mặc định
        if (!response.Recommendations.Any())
        {
            response.Recommendations.Add("Kiểm tra máy móc ngay lập tức");
            response.Recommendations.Add("Liên hệ kỹ thuật viên để đánh giá");
            response.Recommendations.Add("Ghi nhận và theo dõi thêm");
        }

        return response;
    }
}
