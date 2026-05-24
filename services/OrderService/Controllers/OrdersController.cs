using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Events;
using OrderService.Models;
using OrderService.Producers;

namespace OrderService.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    private readonly OrderDbContext _db;
    private readonly IOrderEventProducer _producer;

    public OrdersController(OrderDbContext db, IOrderEventProducer producer)
    {
        _db = db;
        _producer = producer;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? userId)
    {
        var query = _db.Orders.AsQueryable();
        if (userId.HasValue)
            query = query.Where(o => o.UserId == userId.Value);
        return Ok(await query.ToListAsync());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var order = await _db.Orders.FindAsync(id);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        if (request.UserId == Guid.Empty || string.IsNullOrWhiteSpace(request.ProductName) || request.Amount <= 0)
            return BadRequest("UserId, ProductName, and a positive Amount are required.");

        var order = new Order
        {
            UserId = request.UserId,
            ProductName = request.ProductName,
            Amount = request.Amount
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        await _producer.PublishOrderCreatedAsync(new OrderCreatedEvent
        {
            OrderId = order.Id,
            UserId = order.UserId,
            ProductName = order.ProductName,
            Amount = order.Amount,
            CreatedAt = order.CreatedAt
        });

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }
}

public record CreateOrderRequest(Guid UserId, string ProductName, decimal Amount);
