using Microsoft.EntityFrameworkCore;
using OrderService.Models;

namespace OrderService.Data;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.ProductName).IsRequired().HasMaxLength(500);
            e.Property(o => o.Amount).HasColumnType("decimal(18,2)");
            e.Property(o => o.Status).HasMaxLength(50);
            e.HasIndex(o => o.UserId);
        });
    }
}
