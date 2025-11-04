using FmsMonitor.Api.Models;
using FmsMonitor.Api.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace FmsMonitor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AmperageController : ControllerBase
{
    private readonly IMachineAmperageRepository _amperageRepository;
    private readonly ILogger<AmperageController> _logger;

    public AmperageController(
        IMachineAmperageRepository amperageRepository,
        ILogger<AmperageController> logger)
    {
        _amperageRepository = amperageRepository;
        _logger = logger;
    }

    /// <summary>
    /// Lấy dữ liệu Ampe theo mã máy và khoảng thời gian
    /// </summary>
    [HttpGet("machine/{machineCode}")]
    public async Task<ActionResult<IEnumerable<MachineAmperage>>> GetByMachineCode(
        string machineCode,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate)
    {
        try
        {
            var from = fromDate ?? DateTime.UtcNow.AddDays(-1);
            var to = toDate ?? DateTime.UtcNow;

            var data = await _amperageRepository.GetByMachineCodeAsync(machineCode, from, to);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lấy dữ liệu máy {MachineCode}", machineCode);
            return StatusCode(500, new { error = "Lỗi hệ thống", message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy dữ liệu Ampe gần đây theo mã máy
    /// </summary>
    [HttpGet("machine/{machineCode}/recent")]
    public async Task<ActionResult<IEnumerable<MachineAmperage>>> GetRecentData(
        string machineCode,
        [FromQuery] int minutes = 60)
    {
        try
        {
            var data = await _amperageRepository.GetRecentDataAsync(machineCode, minutes);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lấy dữ liệu gần đây máy {MachineCode}", machineCode);
            return StatusCode(500, new { error = "Lỗi hệ thống", message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy dữ liệu Ampe mới nhất theo mã máy
    /// </summary>
    [HttpGet("machine/{machineCode}/latest")]
    public async Task<ActionResult<MachineAmperage>> GetLatest(string machineCode)
    {
        try
        {
            var data = await _amperageRepository.GetLatestAsync(machineCode);
            if (data == null)
            {
                return NotFound(new { error = "Không có dữ liệu cho máy này" });
            }
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lấy dữ liệu mới nhất máy {MachineCode}", machineCode);
            return StatusCode(500, new { error = "Lỗi hệ thống", message = ex.Message });
        }
    }

    /// <summary>
    /// Thêm dữ liệu Ampe mới (cho testing hoặc manual entry)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<MachineAmperage>> AddAmperage([FromBody] MachineAmperage amperage)
    {
        try
        {
            amperage.RecordedAt = DateTime.UtcNow;
            var id = await _amperageRepository.InsertAsync(amperage);
            amperage.Id = id;

            _logger.LogInformation("Đã thêm dữ liệu Ampe cho máy {MachineCode}: {Value}A",
                amperage.MachineCode, amperage.AmperageValue);

            return CreatedAtAction(nameof(GetLatest), new { machineCode = amperage.MachineCode }, amperage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi thêm dữ liệu Ampe");
            return StatusCode(500, new { error = "Lỗi hệ thống", message = ex.Message });
        }
    }
}
