using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Common;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.ProductionJobs;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<ProductionJob> Jobs { get; set; } = new();
    public Dictionary<int, OrderSummary> Orders { get; set; } = new();
    public int? OrderId { get; set; }

    public async Task OnGetAsync(int? orderId)
    {
        OrderId = orderId;
        await SynchronizePrinterStatusesAsync();

        var jobsQuery = _db.ProductionJobs.AsQueryable();
        if (OrderId.HasValue)
        {
            jobsQuery = jobsQuery.Where(j => j.OrderItem != null && j.OrderItem.OrderId == OrderId.Value);
        }

        Jobs = await jobsQuery
            .Include(j => j.OrderItem).ThenInclude(oi => oi!.Product)
            .Include(j => j.OrderItem).ThenInclude(oi => oi!.Service)
            .Include(j => j.OrderItem).ThenInclude(oi => oi!.Files)
            .Include(j => j.PrinterAssignments).ThenInclude(pa => pa.Printer)
            .Include(j => j.JobAssignments).ThenInclude(ja => ja.Employee)
            .ToListAsync();

        // Effective priority adds a one-level boost to active jobs with no meaningful
        // update for 24 hours. Manual priority is never overwritten.
        Jobs = Jobs
            .OrderByDescending(EffectivePriority)
            .ThenBy(j => IsStale(j) ? 0 : 1)
            .ThenBy(j => j.LastUpdatedAt)
            .ThenByDescending(j => j.JobId)
            .ToList();

        var orderIds = Jobs
            .Select(j => j.OrderItem?.OrderId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        Orders = await _db.Orders
            .Where(o => orderIds.Contains(o.OrderId))
            .Include(o => o.Customer)
            .Select(o => new OrderSummary
            {
                OrderId = o.OrderId,
                CreatedAt = o.CreatedAt,
                CustomerName = o.Customer != null ? o.Customer.Name : "Unknown Customer",
                CustomerCode = o.Customer != null ? o.Customer.CustomerCode : null
            })
            .ToDictionaryAsync(o => o.OrderId);
    }

    public string BadgeClass(string status) => WorkflowRules.StatusBadgeClass(status);

    public string JobName(ProductionJob job)
    {
        return job.OrderItem?.Product?.Name
            ?? job.OrderItem?.Service?.Name
            ?? "Production Job";
    }

    public string FileName(ProductionJob job)
    {
        return job.OrderItem?.Files
            .Where(f => f.IsCurrent)
            .OrderByDescending(f => f.FileVersion)
            .Select(f => f.FileName)
            .FirstOrDefault()
            ?? job.OrderItem?.Files.OrderByDescending(f => f.FileVersion).Select(f => f.FileName).FirstOrDefault()
            ?? "No file attached";
    }

    public string PrinterName(ProductionJob job)
    {
        return job.PrinterAssignments
            .OrderByDescending(a => a.Status == "PRINTING")
            .ThenByDescending(a => a.AssignedStart)
            .Select(a => a.Printer?.PrinterCode)
            .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p))
            ?? "Not assigned";
    }


    public int EffectivePriority(ProductionJob job)
    {
        var priority = Math.Clamp(job.Priority, 1, 4);
        return Math.Min(4, priority + (IsStale(job) ? 1 : 0));
    }

    public bool IsStale(ProductionJob job)
    {
        if (job.Status is "COMPLETED" or "FAILED") return false;
        return DateTime.Now - job.LastUpdatedAt >= TimeSpan.FromHours(24);
    }

    public string PriorityLabel(int priority) => priority switch
    {
        4 => "URGENT",
        3 => "HIGH",
        2 => "NORMAL",
        _ => "LOW"
    };

    public string PriorityBadgeClass(int priority) => priority switch
    {
        4 => "bg-danger",
        3 => "bg-warning text-dark",
        2 => "bg-primary",
        _ => "bg-secondary"
    };

    public string EmployeeName(ProductionJob job)
    {
        return job.JobAssignments
            .OrderByDescending(a => a.Role == "PRIMARY" && (a.Status == "ASSIGNED" || a.Status == "IN_PROGRESS"))
            .ThenByDescending(a => a.AssignedStart)
            .Select(a => a.Employee?.FullName)
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
            ?? "Not assigned";
    }

    private Task SynchronizePrinterStatusesAsync() => PrinterStatusSync.SynchronizeAsync(_db);

    public sealed class OrderSummary
    {
        public int OrderId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CustomerName { get; set; } = "Unknown Customer";
        public string? CustomerCode { get; set; }
    }
}
