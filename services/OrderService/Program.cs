using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Producers;

namespace OrderService;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddDbContext<OrderDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
        builder.Services.AddSingleton<IOrderEventProducer, OrderEventProducer>();

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            db.Database.EnsureCreated();
        }

        app.UseSwagger();
        app.UseSwaggerUI();
        app.MapControllers();
        app.Run();
    }
}
