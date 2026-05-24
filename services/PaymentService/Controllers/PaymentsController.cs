using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentService.Data;

namespace PaymentService.Controllers;

[ApiController]
[Route("payments")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentDbContext _db;

    public PaymentsController(PaymentDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? orderId)
    {
        var query = _db.Payments.AsQueryable();
        if (orderId.HasValue)
            query = query.Where(p => p.OrderId == orderId.Value);
        return Ok(await query.ToListAsync());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var payment = await _db.Payments.FindAsync(id);
        return payment is null ? NotFound() : Ok(payment);
    }

    [HttpGet("order/{orderId:guid}")]
    public async Task<IActionResult> GetByOrderId(Guid orderId)
    {
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);
        return payment is null ? NotFound() : Ok(payment);
    }
}
