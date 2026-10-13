using System.ComponentModel.DataAnnotations;

namespace PrintShop3D.Models;

public class Asset
{
    public int AssetId { get; set; }
    [Required, StringLength(50)] public string AssetCode { get; set; } = "";
    [Required, StringLength(50)] public string AssetType { get; set; } = "";
    [Required, StringLength(150)] public string Name { get; set; } = "";
    public DateTime? PurchaseDate { get; set; }
    public decimal PurchaseCost { get; set; }
    public decimal CurrentValue { get; set; }
    [StringLength(100)] public string? Location { get; set; }
    public string Status { get; set; } = "ACTIVE";
}

public class Printer
{
    public int PrinterId { get; set; }
    public int AssetId { get; set; }
    [Required, StringLength(50)] public string PrinterCode { get; set; } = "";
    [Required, StringLength(100)] public string Brand { get; set; } = "";
    [Required, StringLength(100)] public string Model { get; set; } = "";
    [Required, StringLength(50)] public string Technology { get; set; } = "FDM";
    public decimal BuildVolumeX { get; set; }
    public decimal BuildVolumeY { get; set; }
    public decimal BuildVolumeZ { get; set; }
    public string Status { get; set; } = "AVAILABLE";

    // Added by migration 003: free-text quality tier, and a JSON array of
    // {material, rating} objects describing which materials this printer is
    // best suited for (consumed by dbo.usp_SuggestPrinter). The DB enforces
    // BestPrintMaterial IS NULL OR ISJSON(BestPrintMaterial) = 1 - we also
    // validate JSON shape in code before saving so the error shown to the
    // user is friendly rather than a raw SQL constraint violation.
    [StringLength(50)] public string? PrintQuality { get; set; }
    public string? BestPrintMaterial { get; set; }

    public Asset? Asset { get; set; }
}

// One entry of the BestPrintMaterial JSON array: [{"material":"PLA","rating":5}, ...]
public class PrinterMaterialRating
{
    public string Material { get; set; } = "";
    public int Rating { get; set; }
}

// Row shape returned by dbo.usp_SuggestPrinter - not a mapped entity, just a
// DTO for EF Core's SqlQueryRaw<T>.
public class PrinterSuggestion
{
    public string PrinterCode { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public string? PrintQuality { get; set; }
    public int Rating { get; set; }
}

public class PrinterMaterialCompatibility
{
    public int CompatibilityId { get; set; }
    public int PrinterId { get; set; }
    public int MaterialId { get; set; }
    public string? Notes { get; set; }
}

public class AssetMaintenance
{
    public int MaintenanceId { get; set; }
    public int AssetId { get; set; }
    public int TechnicianId { get; set; }
    [Required, StringLength(30)] public string Type { get; set; } = "PREVENTIVE";
    public string? Description { get; set; }
    public DateTime ReportedDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal Cost { get; set; }
    public string Status { get; set; } = "REPORTED";
}
