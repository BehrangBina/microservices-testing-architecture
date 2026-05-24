using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;

namespace NotificationService.Controllers;

[ApiController]
[Route("notifications")]
public class NotificationsController : ControllerBase
{
    private readonly NotificationDbContext _db;

    public NotificationsController(NotificationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? orderId, [FromQuery] string? eventType)
    {
        var query = _db.Notifications.AsQueryable();
        if (orderId.HasValue)
            query = query.Where(n => n.OrderId == orderId.Value);
        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(n => n.EventType == eventType);
        return Ok(await query.OrderByDescending(n => n.ReceivedAt).ToListAsync());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var notification = await _db.Notifications.FindAsync(id);
        return notification is null ? NotFound() : Ok(notification);
    }
}
