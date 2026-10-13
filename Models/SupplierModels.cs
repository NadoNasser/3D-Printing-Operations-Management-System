using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintShop3D.Models;

public class Supplier
{
    public int SupplierId { get; set; }
    [Required, StringLength(50)] public string SupplierCode { get; set; } = "";
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [StringLength(100)] public string? ContactPerson { get; set; }
    [StringLength(25)] public string? Phone { get; set; }
    [StringLength(100)] public string? Email { get; set; }
    [StringLength(255)] public string? Address { get; set; }
    [StringLength(100)] public string? PaymentTerms { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PurchaseOrder
{
    public int PurchaseOrderId { get; set; }
    [Required, StringLength(50)] public string PONumber { get; set; } = "";
    public int SupplierId { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string Status { get; set; } = "DRAFT";
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }

    public Supplier? Supplier { get; set; }
    public List<PurchaseItem> Items { get; set; } = new();
}

public class PurchaseItem
{
    public int PurchaseItemId { get; set; }
    public int PurchaseOrderId { get; set; }
    public int MaterialId { get; set; }
    [Range(0.001, double.MaxValue)] public decimal QuantityOrdered { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; } // computed column in DB, read-only

    public Material? Material { get; set; }

    [NotMapped]
    public decimal QuantityReceivedSoFar { get; set; } // computed in code, not a DB column
}

public class GoodsReceipt
{
    public int ReceiptId { get; set; }
    public int PurchaseOrderId { get; set; }
    public DateTime ReceiptDate { get; set; }
    public int ReceivedBy { get; set; }
    public string? Notes { get; set; }

    public List<GoodsReceiptItem> Items { get; set; } = new();
}

public class GoodsReceiptItem
{
    public int GoodsReceiptItemId { get; set; }
    public int ReceiptId { get; set; }
    public int PurchaseItemId { get; set; }
    [Range(0.001, double.MaxValue)] public decimal QuantityReceived { get; set; }
    public decimal UnitCost { get; set; }
    public string? Notes { get; set; }
}

public class InventoryTransaction
{
    public int TransactionId { get; set; }
    public int MaterialId { get; set; }

    [Required, StringLength(30)]
    public string TransactionType { get; set; } = "ADJUSTMENT";

    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime TransactionDate { get; set; }

    public int? GoodsReceiptItemId { get; set; }
    public int? MaterialUsageId { get; set; }

    // Product sale source.
    public int? OrderItemId { get; set; }

    [StringLength(255)]
    public string? Reason { get; set; }

    public Material? Material { get; set; }
    public OrderItem? OrderItem { get; set; }
}
