using UserService.Models;
using UserService.Data;

namespace IntegrationTests;

/// <summary>
/// Reusable helpers for seeding test data into EF Core DbContext instances.
/// </summary>
public static class TestDataSeeder
{
    public static async Task<User> SeedUserAsync(UserDbContext db, string? name = null, string? email = null)
    {
        var user = new User
        {
            Name  = name  ?? $"Test User {Guid.NewGuid():N}",
            Email = email ?? $"seed+{Guid.NewGuid():N}@test.com"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public static async Task<OrderService.Models.Order> SeedOrderAsync(
        OrderService.Data.OrderDbContext db,
        Guid? userId = null,
        string? productName = null,
        decimal amount = 99.99m)
    {
        var order = new OrderService.Models.Order
        {
            UserId      = userId      ?? Guid.NewGuid(),
            ProductName = productName ?? $"Seeded Product {Guid.NewGuid():N}",
            Amount      = amount
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }
}
