namespace PaymentService.Models;

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Processed";
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
