using FmsMonitor.Api.Models;
using FmsMonitor.Api.Repositories;
using FmsMonitor.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FmsMonitor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MonitoringController : ControllerBase
{
    private readonly IMonitoringConfigurationRepository _configRepository;
    private readonly IAmperageMonitoringService _monitoringService;
    private readonly ILogger<MonitoringController> _logger;

    public MonitoringController(
        IMonitoringConfigurationRepository configRepository,
        IAmperageMonitoringService monitoringService,
        ILogger<MonitoringController> logger)
    {
        _configRepository = configRepository;
        _monitoringService = monitoringService;
        _logger = logger;
    }

    /// <summary>
    /// Lấy tất cả cấu hình giám sát đang hoạt động
    /// </summary>
    [HttpGet("configurations")]
    public async Task<ActionResult<IEnumerable<MonitoringConfiguration>>> GetAllConfigurations()
    {
        try
        {
            var configs = await _configRepository.GetAllActiveAsync();
            return Ok(configs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lấy danh sách cấu hình");
            return StatusCode(500, new { error = "Lỗi hệ thống", message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy cấu hình giám sát theo mã máy
    /// </summary>
    [HttpGet("configurations/{machineCode}")]
    public async Task<ActionResult<MonitoringConfiguration>> GetConfiguration(string machineCode)
    {
        try
        {
            var config = await _configRepository.GetByMachineCodeAsync(machineCode);
            if (config == null)
            {
                return NotFound(new { error = "Không tìm thấy cấu hình cho máy này" });
            }
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lấy cấu hình máy {MachineCode}", machineCode);
            return StatusCode(500, new { error = "Lỗi hệ thống", message = ex.Message });
        }
    }

    /// <summary>
    /// Tạo cấu hình giám sát mới
    /// </summary>
    [HttpPost("configurations")]
    public async Task<ActionResult<MonitoringConfiguration>> CreateConfiguration([FromBody] MonitoringConfiguration config)
    {
        try
        {
            config.CreatedAt = DateTime.UtcNow;
            var id = await _configRepository.InsertAsync(config);
            config.Id = id;

            _logger.LogInformation("Đã tạo cấu hình mới cho máy {MachineCode}", config.MachineCode);
            return CreatedAtAction(nameof(GetConfiguration), new { machineCode = config.MachineCode }, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi tạo cấu hình mới");
            return StatusCode(500, new { error = "Lỗi hệ thống", message = ex.Message });
        }
    }

    /// <summary>
    /// Cập nhật cấu hình giám sát
    /// </summary>
    [HttpPut("configurations/{id}")]
    public async Task<ActionResult> UpdateConfiguration(int id, [FromBody] MonitoringConfiguration config)
    {
        try
        {
            config.Id = id;
            config.UpdatedAt = DateTime.UtcNow;
            await _configRepository.UpdateAsync(config);

            _logger.LogInformation("Đã cập nhật cấu hình máy {MachineCode}", config.MachineCode);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi cập nhật cấu hình {Id}", id);
            return StatusCode(500, new { error = "Lỗi hệ thống", message = ex.Message });
        }
    }

    /// <summary>
    /// Xóa cấu hình giám sát
    /// </summary>
    [HttpDelete("configurations/{id}")]
    public async Task<ActionResult> DeleteConfiguration(int id)
    {
        try
        {
            await _configRepository.DeleteAsync(id);
            _logger.LogInformation("Đã xóa cấu hình {Id}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xóa cấu hình {Id}", id);
            return StatusCode(500, new { error = "Lỗi hệ thống", message = ex.Message });
        }
    }

    /// <summary>
    /// Phân tích dao động của máy
    /// </summary>
    [HttpGet("analyze/{machineCode}")]
    public async Task<ActionResult<FluctuationAnalysis>> AnalyzeFluctuation(string machineCode)
    {
        try
        {
            var config = await _configRepository.GetByMachineCodeAsync(machineCode);
            if (config == null)
            {
                return NotFound(new { error = "Không tìm thấy cấu hình cho máy này" });
            }

            var analysis = await _monitoringService.AnalyzeFluctuationAsync(machineCode, config);
            return Ok(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi phân tích máy {MachineCode}", machineCode);
            return StatusCode(500, new { error = "Lỗi hệ thống", message = ex.Message });
        }
    }

    /// <summary>
    /// Kích hoạt giám sát thủ công cho tất cả các máy
    /// </summary>
    [HttpPost("run")]
    public async Task<ActionResult> RunMonitoring()
    {
        try
        {
            await _monitoringService.MonitorAllMachinesAsync();
            _logger.LogInformation("Đã chạy giám sát thủ công");
            return Ok(new { message = "Giám sát hoàn tất" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi chạy giám sát");
            return StatusCode(500, new { error = "Lỗi hệ thống", message = ex.Message });
        }
    }
}
