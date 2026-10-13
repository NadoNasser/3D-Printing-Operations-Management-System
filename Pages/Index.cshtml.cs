using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;
    public int OpenOrdersCount { get; set; }
    public int AvailablePrintersCount { get; set; }
    public int BusyPrintersCount { get; set; }
    public int TotalPrintersCount { get; set; }
    public int QueuedJobsCount { get; set; }
    public int InProgressJobsCount { get; set; }
    public int QualityCheckJobsCount { get; set; }
    public int OverdueJobsCount { get; set; }
    public int UnpaidOrdersCount { get; set; }
    public List<MaterialStockOnHand> LowStockMaterials { get; set; } = new();

    public async Task OnGetAsync()
    {
        await SynchronizePrinterStatusesAsync();
        var closedStatuses = new[] { "COMPLETED", "CANCELED", "DELIVERED" };
        OpenOrdersCount = await _db.Orders.CountAsync(o => !closedStatuses.Contains(o.Status));
        TotalPrintersCount = await _db.Printers.CountAsync();
        AvailablePrintersCount = await _db.Printers.CountAsync(p => p.Status == "AVAILABLE");
        BusyPrintersCount = await _db.Printers.CountAsync(p => p.Status == "BUSY");
        QueuedJobsCount = await _db.ProductionJobs.CountAsync(j => j.Status == "QUEUED" || j.Status == "SCHEDULED");
        InProgressJobsCount = await _db.ProductionJobs.CountAsync(j => j.Status == "IN_PROGRESS");
        QualityCheckJobsCount = await _db.ProductionJobs.CountAsync(j => j.Status == "QUALITY_CHECK");
        var now = DateTime.Now;
        var activeJobs = await _db.ProductionJobs
            .Where(j => j.EndTime == null && j.StartTime != null && j.Status != "COMPLETED" && j.Status != "FAILED")
            .Select(j => new { j.StartTime, j.EstimatedTime })
            .ToListAsync();
        OverdueJobsCount = activeJobs.Count(j => j.StartTime!.Value.AddHours((double)j.EstimatedTime) < now);
        var orders = await _db.Orders.Include(o => o.Items).Include(o => o.Payments).ToListAsync();
        UnpaidOrdersCount = orders.Count(o => o.Balance > 0.005m && o.Status != "CANCELED");
        LowStockMaterials = await _db.MaterialStockOnHand.Where(m => m.NeedsReorder == 1).OrderBy(m => m.MaterialName).ToListAsync();
    }

    private async Task SynchronizePrinterStatusesAsync()
    {
        var now = DateTime.Now;
        var printers = await _db.Printers.ToListAsync();
        var assignments = await _db.PrinterAssignments.Where(a => a.Status == "SCHEDULED" || a.Status == "PRINTING").ToListAsync();
        var changed = false;
        foreach (var printer in printers)
        {
            if (printer.Status is "MAINTENANCE" or "OUT_OF_SERVICE" or "INACTIVE") continue;
            var busy = assignments.Any(a => a.PrinterId == printer.PrinterId && a.AssignedStart <= now && a.AssignedEnd > now);
            var desired = busy ? "BUSY" : "AVAILABLE";
            if (printer.Status != desired) { printer.Status = desired; changed = true; }
        }
        if (changed) await _db.SaveChangesAsync();
    }
}
