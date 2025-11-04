using FmsMonitor.Api.Models;
using FmsMonitor.Api.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace FmsMonitor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly IAlertRepository _alertRepository;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(IAlertRepository alertRepository, ILogger<AlertsController> logger)
    {
        _alertRepository = alertRepository;
        _logger = logger;
    }

    /// <summary>
    /// Lấy tất cả cảnh báo chưa giải quyết
    /// </summary>
    [HttpGet("unresolved")]
    public async Task<ActionResult<IEnumerable<Alert>>> GetUnresolved()
    {
        try
        {
            var alerts = await _alertRepository.GetUnresolvedAsync();
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lấy danh sách cảnh báo chưa giải quyết");
            return StatusCode(500, new { error = "Lỗi hệ thống", message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy cảnh báo gần đây theo mã máy
    /// </summary>
    [HttpGet("machine/{machineCode}")]
    public async Task<ActionResult<IEnumerable<Alert>>> GetByMachineCode(string machineCode, [FromQuery] int hours = 24)
    {
        try
        {
            var alerts = await _alertRepository.GetRecentByMachineCodeAsync(machineCode, hours);
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lấy cảnh báo máy {MachineCode}", machineCode);
            return StatusCode(500, new { error = "Lỗi hệ thống", message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy chi tiết cảnh báo
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Alert>> GetById(int id)
    {
        try
        {
            var alert = await _alertRepository.GetByIdAsync(id);
            if (alert == null)
            {
                return NotFound(new { error = "Không tìm thấy cảnh báo" });
            }
            return Ok(alert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lấy cảnh báo {Id}", id);
            return StatusCode(500, new { error = "Lỗi hệ thống", message = ex.Message });
        }
    }

    /// <summary>
    /// Đánh dấu cảnh báo đã giải quyết
    /// </summary>
    [HttpPut("{id}/resolve")]
    public async Task<ActionResult> ResolveAlert(int id, [FromBody] ResolveAlertRequest request)
    {
        try
        {
            var alert = await _alertRepository.GetByIdAsync(id);
            if (alert == null)
            {
                return NotFound(new { error = "Không tìm thấy cảnh báo" });
            }

            alert.IsResolved = true;
            alert.ResolvedAt = DateTime.UtcNow;
            alert.ResolvedBy = request.ResolvedBy;

            await _alertRepository.UpdateAsync(alert);

            _logger.LogInformation("Cảnh báo {Id} đã được giải quyết bởi {User}", id, request.ResolvedBy);
            return Ok(alert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi giải quyết cảnh báo {Id}", id);
            return StatusCode(500, new { error = "Lỗi hệ thống", message = ex.Message });
        }
    }
}

public class ResolveAlertRequest
{
    public string ResolvedBy { get; set; } = string.Empty;
}
