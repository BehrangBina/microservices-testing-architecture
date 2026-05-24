namespace PaymentService.Events;

public class PaymentProcessedEvent
{
    public Guid PaymentId { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Processed";
    public DateTime ProcessedAt { get; set; }
}
