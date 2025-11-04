using FmsMonitor.Api.Models;
using FmsMonitor.Api.Repositories;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Text;

namespace FmsMonitor.Api.Services;

/// <summary>
/// Service gửi email cảnh báo
/// </summary>
public interface IEmailNotificationService
{
    Task SendAlertAsync(Alert alert, List<string> recommendations);
    Task<bool> TestConnectionAsync();
}

public class EmailNotificationService : IEmailNotificationService
{
    private readonly IEmailRecipientRepository _recipientRepository;
    private readonly IAlertRepository _alertRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        IEmailRecipientRepository recipientRepository,
        IAlertRepository alertRepository,
        IConfiguration configuration,
        ILogger<EmailNotificationService> logger)
    {
        _recipientRepository = recipientRepository;
        _alertRepository = alertRepository;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAlertAsync(Alert alert, List<string> recommendations)
    {
        try
        {
            _logger.LogInformation("Gửi email cảnh báo cho máy {MachineCode}, Level: {Level}",
                alert.MachineCode, alert.Level);

            // Lấy danh sách người nhận theo mức cảnh báo
            var recipients = await _recipientRepository.GetByAlertLevelAsync(alert.Level);
            var recipientList = recipients.ToList();

            if (!recipientList.Any())
            {
                _logger.LogWarning("Không có người nhận cho mức cảnh báo {Level}", alert.Level);
                return;
            }

            // Tạo email content
            var emailBody = BuildEmailBody(alert, recommendations);
            var subject = BuildEmailSubject(alert);

            // Gửi email
            await SendEmailAsync(recipientList, subject, emailBody);

            // Cập nhật trạng thái đã gửi
            alert.IsNotified = true;
            alert.NotifiedAt = DateTime.UtcNow;
            await _alertRepository.UpdateAsync(alert);

            _logger.LogInformation("Đã gửi email cảnh báo đến {Count} người nhận", recipientList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi gửi email cảnh báo cho máy {MachineCode}", alert.MachineCode);
            throw;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var smtpHost = _configuration["Email:SmtpHost"];
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");

            using var client = new SmtpClient();
            await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);

            var username = _configuration["Email:Username"];
            var password = _configuration["Email:Password"];

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                await client.AuthenticateAsync(username, password);
            }

            await client.DisconnectAsync(true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi kết nối SMTP server");
            return false;
        }
    }

    private async Task SendEmailAsync(List<EmailRecipient> recipients, string subject, string body)
    {
        var smtpHost = _configuration["Email:SmtpHost"];
        var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
        var fromEmail = _configuration["Email:FromEmail"];
        var fromName = _configuration["Email:FromName"] ?? "FMS Monitor System";
        var username = _configuration["Email:Username"];
        var password = _configuration["Email:Password"];

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));

        foreach (var recipient in recipients)
        {
            message.To.Add(new MailboxAddress(recipient.Name, recipient.Email));
        }

        message.Subject = subject;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = body
        };

        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                await client.AuthenticateAsync(username, password);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi gửi email qua SMTP");
            throw;
        }
    }

    private string BuildEmailSubject(Alert alert)
    {
        var levelText = alert.Level switch
        {
            AlertLevel.Info => "THÔNG TIN",
            AlertLevel.Warning => "CẢNH BÁO",
            AlertLevel.Critical => "NGHIÊM TRỌNG",
            AlertLevel.Emergency => "KHẨN CẤP",
            _ => "THÔNG BÁO"
        };

        return $"[{levelText}] FMS Monitor - {alert.MachineName} ({alert.MachineCode})";
    }

    private string BuildEmailBody(Alert alert, List<string> recommendations)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset='utf-8'>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }");
        sb.AppendLine(".container { max-width: 600px; margin: 0 auto; padding: 20px; }");
        sb.AppendLine(".header { background: #2c3e50; color: white; padding: 20px; text-align: center; }");
        sb.AppendLine(".content { background: #f4f4f4; padding: 20px; margin-top: 20px; }");
        sb.AppendLine(".alert-info { border-left: 4px solid #3498db; padding: 10px; margin: 10px 0; background: white; }");
        sb.AppendLine(".alert-warning { border-left: 4px solid #f39c12; padding: 10px; margin: 10px 0; background: white; }");
        sb.AppendLine(".alert-critical { border-left: 4px solid #e74c3c; padding: 10px; margin: 10px 0; background: white; }");
        sb.AppendLine(".alert-emergency { border-left: 4px solid #c0392b; padding: 10px; margin: 10px 0; background: white; }");
        sb.AppendLine(".info-row { padding: 8px 0; border-bottom: 1px solid #ddd; }");
        sb.AppendLine(".info-label { font-weight: bold; color: #2c3e50; }");
        sb.AppendLine(".recommendations { background: #fff3cd; padding: 15px; margin-top: 20px; border-radius: 5px; }");
        sb.AppendLine(".footer { text-align: center; margin-top: 20px; font-size: 12px; color: #7f8c8d; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class='container'>");

        // Header
        sb.AppendLine("<div class='header'>");
        sb.AppendLine($"<h1>⚠️ Cảnh báo FMS Monitor</h1>");
        sb.AppendLine($"<p>{GetAlertLevelText(alert.Level)}</p>");
        sb.AppendLine("</div>");

        // Content
        sb.AppendLine("<div class='content'>");

        var alertClass = alert.Level switch
        {
            AlertLevel.Info => "alert-info",
            AlertLevel.Warning => "alert-warning",
            AlertLevel.Critical => "alert-critical",
            AlertLevel.Emergency => "alert-emergency",
            _ => "alert-info"
        };

        sb.AppendLine($"<div class='{alertClass}'>");

        sb.AppendLine("<div class='info-row'>");
        sb.AppendLine("<span class='info-label'>Máy:</span> ");
        sb.AppendLine($"{alert.MachineName} ({alert.MachineCode})");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='info-row'>");
        sb.AppendLine("<span class='info-label'>Loại cảnh báo:</span> ");
        sb.AppendLine(GetAlertTypeText(alert.Type));
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='info-row'>");
        sb.AppendLine("<span class='info-label'>Giá trị hiện tại:</span> ");
        sb.AppendLine($"<strong>{alert.CurrentValue}A</strong>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='info-row'>");
        sb.AppendLine("<span class='info-label'>Thời gian phát hiện:</span> ");
        sb.AppendLine($"{alert.DetectedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='info-row'>");
        sb.AppendLine("<span class='info-label'>Thông báo:</span><br>");
        sb.AppendLine($"<p>{alert.Message}</p>");
        sb.AppendLine("</div>");

        if (!string.IsNullOrEmpty(alert.AiAnalysis))
        {
            sb.AppendLine("<div class='info-row'>");
            sb.AppendLine("<span class='info-label'>🤖 Phân tích AI:</span><br>");
            sb.AppendLine($"<p>{alert.AiAnalysis}</p>");
            sb.AppendLine("</div>");
        }

        sb.AppendLine("</div>"); // Close alert div

        // Recommendations
        if (recommendations.Any())
        {
            sb.AppendLine("<div class='recommendations'>");
            sb.AppendLine("<h3>📋 Khuyến nghị xử lý:</h3>");
            sb.AppendLine("<ul>");
            foreach (var rec in recommendations)
            {
                sb.AppendLine($"<li>{rec}</li>");
            }
            sb.AppendLine("</ul>");
            sb.AppendLine("</div>");
        }

        sb.AppendLine("</div>"); // Close content

        // Footer
        sb.AppendLine("<div class='footer'>");
        sb.AppendLine("<p>Email này được gửi tự động từ hệ thống FMS Monitor</p>");
        sb.AppendLine($"<p>© {DateTime.Now.Year} FMS Monitor System</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("</div>"); // Close container
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private string GetAlertLevelText(AlertLevel level)
    {
        return level switch
        {
            AlertLevel.Info => "THÔNG TIN",
            AlertLevel.Warning => "CẢNH BÁO",
            AlertLevel.Critical => "NGHIÊM TRỌNG",
            AlertLevel.Emergency => "KHẨN CẤP",
            _ => "KHÔNG XÁC ĐỊNH"
        };
    }

    private string GetAlertTypeText(AlertType type)
    {
        return type switch
        {
            AlertType.BelowMinimum => "Dưới ngưỡng tối thiểu",
            AlertType.AboveMaximum => "Vượt ngưỡng tối đa",
            AlertType.AbnormalFluctuation => "Dao động bất thường",
            AlertType.NoData => "Không có dữ liệu",
            _ => "Không xác định"
        };
    }
}
