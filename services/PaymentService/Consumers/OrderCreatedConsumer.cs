using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.Events;
using PaymentService.Models;
using PaymentService.Producers;

namespace PaymentService.Consumers;

/// <summary>
/// Background service that consumes OrderCreated events from Kafka,
/// creates a Payment record, and publishes a PaymentProcessed event.
/// </summary>
public class OrderCreatedConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPaymentEventProducer _producer;
    private readonly ILogger<OrderCreatedConsumer> _logger;
    private readonly string _bootstrapServers;
    private const string Topic = "order-created";
    private const string GroupId = "payment-service-group";

    public OrderCreatedConsumer(
        IServiceScopeFactory scopeFactory,
        IPaymentEventProducer producer,
        ILogger<OrderCreatedConsumer> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _producer = producer;
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

        _logger.LogInformation("OrderCreatedConsumer started, listening on topic {Topic}", Topic);
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result is null) continue;

                var evt = JsonSerializer.Deserialize<OrderCreatedEventDto>(result.Message.Value);
                if (evt is null) continue;

                await ProcessAsync(evt, stoppingToken);
                consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume error");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }

        consumer.Close();
    }

    private async Task ProcessAsync(OrderCreatedEventDto evt, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        // Idempotency: skip if payment already exists for this order
        if (await db.Payments.AnyAsync(p => p.OrderId == evt.OrderId, ct))
        {
            _logger.LogWarning("Payment already exists for OrderId {OrderId}, skipping", evt.OrderId);
            return;
        }

        var payment = new Payment
        {
            OrderId = evt.OrderId,
            Amount = evt.Amount,
            Status = "Processed"
        };

        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);

        await _producer.PublishPaymentProcessedAsync(new PaymentProcessedEvent
        {
            PaymentId = payment.Id,
            OrderId = payment.OrderId,
            Amount = payment.Amount,
            Status = payment.Status,
            ProcessedAt = payment.ProcessedAt
        }, ct);

        _logger.LogInformation("Payment {PaymentId} processed for OrderId {OrderId}", payment.Id, payment.OrderId);
    }
}

// Internal DTO — mirrors OrderCreatedEvent from OrderService without a shared library
internal record OrderCreatedEventDto(Guid OrderId, Guid UserId, string ProductName, decimal Amount, DateTime CreatedAt);
