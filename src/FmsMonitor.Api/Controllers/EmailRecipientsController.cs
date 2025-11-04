using FmsMonitor.Api.Models;
using FmsMonitor.Api.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace FmsMonitor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmailRecipientsController : ControllerBase
{
    private readonly IEmailRecipientRepository _recipientRepository;
    private readonly ILogger<EmailRecipientsController> _logger;

    public EmailRecipientsController(
        IEmailRecipientRepository recipientRepository,
        ILogger<EmailRecipientsController> logger)
    {
        _recipientRepository = recipientRepository;
        _logger = logger;
    }

    /// <summary>
    /// Lấy tất cả người nhận email đang hoạt động
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EmailRecipient>>> GetAll()
    {
        try
        {
            var recipients = await _recipientRepository.GetAllActiveAsync();
            return Ok(recipients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lấy danh sách người nhận");
            return StatusCode(500, new { error = "Lỗi hệ thống", message = ex.Message });
        }
    }

    /// <summary>
    /// Thêm người nhận email mới
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<EmailRecipient>> Create([FromBody] EmailRecipient recipient)
    {
        try
        {
            recipient.CreatedAt = DateTime.UtcNow;
            var id = await _recipientRepository.InsertAsync(recipient);
            recipient.Id = id;

            _logger.LogInformation("Đã thêm người nhận: {Email}", recipient.Email);
            return CreatedAtAction(nameof(GetAll), recipient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi thêm người nhận");
            return StatusCode(500, new { error = "Lỗi hệ thống", message = ex.Message });
        }
    }

    /// <summary>
    /// Cập nhật người nhận email
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, [FromBody] EmailRecipient recipient)
    {
        try
        {
            recipient.Id = id;
            await _recipientRepository.UpdateAsync(recipient);

            _logger.LogInformation("Đã cập nhật người nhận: {Email}", recipient.Email);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi cập nhật người nhận {Id}", id);
            return StatusCode(500, new { error = "Lỗi hệ thống", message = ex.Message });
        }
    }
}
