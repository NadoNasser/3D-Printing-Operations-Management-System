using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintShop3D.Models;

public class Product
{
    public int ProductId { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = "";

    [StringLength(100)]
    public string? Category { get; set; }

    public string? Description { get; set; }

    public decimal BasePrice { get; set; }

    [StringLength(20)]
    public string Unit { get; set; } = "PCS";

    // Product sales consume inventory from this material.
    public int? MaterialId { get; set; }

    public Material? Material { get; set; }
}

public class Service
{
    public int ServiceId { get; set; }
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [StringLength(100)] public string? Category { get; set; }
    public string? Description { get; set; }
    public decimal BaseRate { get; set; }
    [StringLength(20)] public string PricingUnit { get; set; } = "HOURS";
}

public class Material
{
    public int MaterialId { get; set; }
    [Required, StringLength(50)] public string SKU { get; set; } = "";
    [Required, StringLength(50)] public string MaterialType { get; set; } = "";
    [StringLength(100)] public string? Brand { get; set; }
    [Required, StringLength(150)] public string MaterialName { get; set; } = "";
    [StringLength(50)] public string? Color { get; set; }
    public decimal? Diameter { get; set; }
    [StringLength(20)] public string Unit { get; set; } = "KG";
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal SafetyStock { get; set; }
    public decimal ReorderLevel { get; set; }
}

// Maps to the read-only VW_MATERIAL_STOCK_ON_HAND view (see create_database.sql).
public class MaterialStockOnHand
{
    public int MaterialId { get; set; }
    public string SKU { get; set; } = "";
    public string MaterialName { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal SafetyStock { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal CurrentStockLevel { get; set; }
    public int NeedsReorder { get; set; } // 1/0 from the view's CASE expression (SQL Server int, not bit)

    [NotMapped]
    public bool IsLowStock => NeedsReorder == 1;
}
