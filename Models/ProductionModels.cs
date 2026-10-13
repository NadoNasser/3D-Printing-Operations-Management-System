using System.ComponentModel.DataAnnotations;

namespace PrintShop3D.Models;

public class ProductionJob
{
    public int JobId { get; set; }
    public int OrderItemId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    [Range(0.01, double.MaxValue)] public decimal EstimatedTime { get; set; }
    [Range(0, double.MaxValue)] public decimal ActualTime { get; set; }
    [Range(1, int.MaxValue)] public int Quantity { get; set; }
    public string Status { get; set; } = "QUEUED";
    /// <summary>Manual priority: 1=Low, 2=Normal, 3=High, 4=Urgent.</summary>
    [Range(1, 4)] public int Priority { get; set; } = 2;
    /// <summary>Last meaningful production-job update used for stale-job escalation.</summary>
    public DateTime LastUpdatedAt { get; set; } = DateTime.Now;

    public OrderItem? OrderItem { get; set; }
    public List<JobAssignment> JobAssignments { get; set; } = new();
    public List<PrinterAssignment> PrinterAssignments { get; set; } = new();
    public List<MaterialUsage> MaterialUsages { get; set; } = new();
    public JobCostBreakdown? CostBreakdown { get; set; }
}

public class JobAssignment
{
    public int JobAssignmentId { get; set; }
    public int JobId { get; set; }
    public int EmployeeId { get; set; }
    public string Role { get; set; } = "PRIMARY";
    public DateTime AssignedStart { get; set; }
    public DateTime AssignedEnd { get; set; }
    public string Status { get; set; } = "ASSIGNED";

    public Employee? Employee { get; set; }
}

public class PrinterAssignment
{
    public int PrinterAssignmentId { get; set; }
    public int JobId { get; set; }
    public int PrinterId { get; set; }
    public DateTime AssignedStart { get; set; }
    public DateTime AssignedEnd { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
    public string Status { get; set; } = "SCHEDULED";

    public Printer? Printer { get; set; }
}

public class MaterialUsage
{
    public int MaterialUsageId { get; set; }
    public int JobId { get; set; }
    public int MaterialId { get; set; }
    [Range(0.001, double.MaxValue)] public decimal QuantityUsed { get; set; }
    public decimal WasteQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime UsageDate { get; set; }

    public Material? Material { get; set; }
}

public class EmployeeEvaluation
{
    public int EvaluationId { get; set; }
    public int EmployeeId { get; set; }
    public int? JobAssignmentId { get; set; }
    public DateTime EvaluationDate { get; set; }
    public bool OnTimeDelivery { get; set; }
    [Required, StringLength(20)] public string QualityResult { get; set; } = "PASS";
    [Required, StringLength(20)] public string Rating { get; set; } = "GOOD";
    public string? Notes { get; set; }
    public int EvaluatedBy { get; set; }
}

public class PayrollAdjustment
{
    public int AdjustmentId { get; set; }
    public int EmployeeId { get; set; }
    public int? EvaluationId { get; set; }
    [Required, StringLength(20)] public string AdjustmentType { get; set; } = "BONUS";
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string Status { get; set; } = "PENDING";
    public int? ApprovedBy { get; set; }
}

public class JobCostBreakdown
{
    public int BreakdownId { get; set; }
    public int JobId { get; set; }
    public decimal MaterialCost { get; set; }
    public decimal MachineCost { get; set; }
    public decimal LaborCost { get; set; }
    public decimal DesignCost { get; set; }
    public decimal PostProcessingCost { get; set; }
    public decimal OutsourcingCost { get; set; }
    public decimal DeliveryCost { get; set; }
    public decimal ProfitMarginPercent { get; set; }
    public decimal FinalPrice { get; set; } // computed column
}
