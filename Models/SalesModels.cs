using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintShop3D.Models;

public class Quotation
{
    public int QuotationId { get; set; }
    public int CustomerId { get; set; }
    [Required, StringLength(50)] public string QuoteNumber { get; set; } = "";
    public DateTime IssueDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string Status { get; set; } = "DRAFT";
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
}

public class QuotationItem
{
    public int QuotationItemId { get; set; }
    public int QuotationId { get; set; }
    public int? ProductId { get; set; }
    public int? ServiceId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class Order
{
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public int? QuotationId { get; set; }
    public string Status { get; set; } = "DRAFT";
    public DateTime CreatedAt { get; set; }

    public Customer? Customer { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public List<Delivery> Deliveries { get; set; } = new();
    public List<OrderStatusHistory> StatusHistory { get; set; } = new();

    [NotMapped] public decimal TotalAmount => Items.Sum(i => i.LineTotal);
    [NotMapped] public decimal TotalPaid => Payments.Where(p => p.PaymentStatus == "COMPLETED").Sum(p => p.Amount);
    [NotMapped] public decimal Balance => TotalAmount - TotalPaid;
}

public class OrderItem
{
    public int OrderItemId { get; set; }
    public int OrderId { get; set; }
    public int? QuotationItemId { get; set; }
    public int? ProductId { get; set; }
    public int? ServiceId { get; set; }
    [Range(1, int.MaxValue)] public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; } // computed column

    public Product? Product { get; set; }
    public Service? Service { get; set; }
    public List<ProductionJob> ProductionJobs { get; set; } = new();
    public List<PrintFile> Files { get; set; } = new();
}

public class Payment
{
    public int PaymentId { get; set; }
    public int OrderId { get; set; }
    [Range(0.01, double.MaxValue)] public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    [Required, StringLength(50)] public string PaymentMethod { get; set; } = "CASH";
    public string PaymentStatus { get; set; } = "PENDING";
    [StringLength(100)] public string? Reference { get; set; }
    public string? Notes { get; set; }

    public Order? Order { get; set; }
}

public class Delivery
{
    public int DeliveryId { get; set; }
    public int OrderId { get; set; }
    public int? AddressId { get; set; }
    [Required, StringLength(20)] public string Method { get; set; } = "PICKUP";
    [StringLength(100)] public string? Carrier { get; set; }
    [StringLength(100)] public string? TrackingNumber { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime? DeliveredDate { get; set; }
    public string Status { get; set; } = "PENDING";
    public string? Notes { get; set; }

    public CustomerAddress? Address { get; set; }
}

public class OrderStatusHistory
{
    public int HistoryId { get; set; }
    public int OrderId { get; set; }
    [StringLength(30)] public string? FromStatus { get; set; }
    [Required, StringLength(30)] public string ToStatus { get; set; } = "";
    public int ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? Reason { get; set; }

    public Employee? ChangedByEmployee { get; set; }
}
