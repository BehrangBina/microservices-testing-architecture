using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Models;

namespace NotificationService.Consumers;

public class OrderCreatedConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderCreatedConsumer> _logger;
    private readonly string _bootstrapServers;
    private const string Topic = "order-created";
    private const string GroupId = "notification-service-order-group";

    public OrderCreatedConsumer(IServiceScopeFactory scopeFactory, ILogger<OrderCreatedConsumer> logger, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(Topic);

        _logger.LogInformation("NotificationService OrderCreatedConsumer started");
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result is null) continue;

                var payload = JsonSerializer.Deserialize<JsonElement>(result.Message.Value);
                var orderId = TryGetOrderId(payload);

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
                db.Notifications.Add(new Notification
                {
                    EventType = "OrderCreated",
                    OrderId = orderId,
                    Payload = result.Message.Value
                });
                await db.SaveChangesAsync(stoppingToken);
                consumer.Commit(result);

                _logger.LogInformation("Notification logged for OrderCreated: OrderId={OrderId}", orderId);
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume error in OrderCreatedConsumer");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }

        consumer.Close();
    }

    private static Guid? TryGetOrderId(JsonElement payload)
    {
        if (TryReadGuid(payload, "orderId", out var guid) || TryReadGuid(payload, "OrderId", out guid))
        {
            return guid;
        }

        return null;
    }

    private static bool TryReadGuid(JsonElement payload, string propertyName, out Guid value)
    {
        value = Guid.Empty;

        if (!payload.TryGetProperty(propertyName, out var prop))
        {
            return false;
        }

        if (prop.ValueKind == JsonValueKind.String)
        {
            return Guid.TryParse(prop.GetString(), out value);
        }

        if (prop.ValueKind == JsonValueKind.Null)
        {
            return false;
        }

        try
        {
            value = prop.GetGuid();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
