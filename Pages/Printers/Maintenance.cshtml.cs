using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Common;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Printers;

public class MaintenanceModel : PageModel
{
    private readonly AppDbContext _db;
    public MaintenanceModel(AppDbContext db) => _db = db;

    public Printer Printer { get; set; } = new();
    public List<MaintenanceRow> History { get; set; } = new();
    public List<Employee> Technicians { get; set; } = new();

    [BindProperty] public int PrinterId { get; set; }
    [BindProperty] public int TechnicianId { get; set; }
    [BindProperty] public string Type { get; set; } = "PREVENTIVE";
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public decimal Cost { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostAddAsync()
    {
        if (!await LoadAsync(PrinterId)) return NotFound();

        Type = (Type ?? "").Trim().ToUpperInvariant();
        if (!DomainConstants.MaintenanceTypes.Contains(Type))
            ModelState.AddModelError(nameof(Type), "Select a valid maintenance type.");

        if (TechnicianId <= 0 || !await _db.Employees.AnyAsync(e => e.EmployeeId == TechnicianId && e.Status == "ACTIVE"))
            ModelState.AddModelError(nameof(TechnicianId), "Select an active technician.");

        if (Cost < 0)
            ModelState.AddModelError(nameof(Cost), "Cost cannot be negative.");

        if (!ModelState.IsValid)
        {
            await LoadHistoryAndTechniciansAsync(Printer.AssetId);
            return Page();
        }

        var maintenance = new AssetMaintenance
        {
            AssetId = Printer.AssetId,
            TechnicianId = TechnicianId,
            Type = Type,
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            Cost = Cost,
            Status = "REPORTED"
        };

        _db.AssetMaintenances.Add(maintenance);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Maintenance record added for printer {Printer.PrinterCode}.";
        return RedirectToPage(new { id = Printer.PrinterId });
    }

    public async Task<IActionResult> OnPostStatusAsync(int id, int maintenanceId, string status)
    {
        if (!await LoadAsync(id)) return NotFound();

        status = (status ?? "").Trim().ToUpperInvariant();
        if (!DomainConstants.MaintenanceStatuses.Contains(status))
            return ErrorRedirect(id, "That maintenance status is not valid.");

        var maintenance = await _db.AssetMaintenances
            .FirstOrDefaultAsync(m => m.MaintenanceId == maintenanceId && m.AssetId == Printer.AssetId);
        if (maintenance == null) return NotFound();

        if (!IsValidTransition(maintenance.Status, status))
            return ErrorRedirect(id, $"Maintenance cannot move from {maintenance.Status} to {status}.");

        if (status == "IN_PROGRESS")
        {
            var hasActiveAssignment = await _db.PrinterAssignments.AnyAsync(a =>
                a.PrinterId == Printer.PrinterId &&
                (a.Status == "SCHEDULED" || a.Status == "PRINTING") &&
                a.AssignedEnd > DateTime.Now);

            if (hasActiveAssignment)
                return ErrorRedirect(id, "This printer has an active scheduled/printing job. Complete that job before starting maintenance.");
        }

        maintenance.Status = status;
        await _db.SaveChangesAsync();
        await SynchronizePrinterStatusAsync(Printer);

        TempData["Success"] = $"Maintenance status changed to {status}.";
        return RedirectToPage(new { id });
    }

    public string BadgeClass(string status) => WorkflowRules.StatusBadgeClass(status);
    public IEnumerable<string> MaintenanceTypes => DomainConstants.MaintenanceTypes;
    public IEnumerable<string> MaintenanceStatuses => DomainConstants.MaintenanceStatuses;

    private bool IsValidTransition(string current, string next) =>
        string.Equals(current, next, StringComparison.OrdinalIgnoreCase) ||
        (current == "REPORTED" && (next == "IN_PROGRESS" || next == "COMPLETED")) ||
        (current == "IN_PROGRESS" && next == "COMPLETED");

    private async Task<bool> LoadAsync(int printerId)
    {
        var printer = await _db.Printers.FindAsync(printerId);
        if (printer == null) return false;

        Printer = printer;
        PrinterId = printer.PrinterId;
        await LoadHistoryAndTechniciansAsync(printer.AssetId);
        return true;
    }

    private async Task LoadHistoryAndTechniciansAsync(int assetId)
    {
        History = await (
            from m in _db.AssetMaintenances
            join e in _db.Employees on m.TechnicianId equals e.EmployeeId
            where m.AssetId == assetId
            orderby m.MaintenanceId descending
            select new MaintenanceRow
            {
                MaintenanceId = m.MaintenanceId,
                Type = m.Type,
                Description = m.Description,
                TechnicianName = e.FullName,
                Cost = m.Cost,
                Status = m.Status
            }).ToListAsync();

        Technicians = await _db.Employees
            .Where(e => e.Status == "ACTIVE")
            .OrderBy(e => e.FullName)
            .ToListAsync();
    }

    private async Task SynchronizePrinterStatusAsync(Printer printer)
    {
        if (printer.Status is "OUT_OF_SERVICE" or "INACTIVE") return;

        var hasMaintenanceInProgress = await _db.AssetMaintenances.AnyAsync(m =>
            m.AssetId == printer.AssetId && m.Status == "IN_PROGRESS");

        if (hasMaintenanceInProgress)
        {
            printer.Status = "MAINTENANCE";
        }
        else
        {
            var now = DateTime.Now;
            var busy = await _db.PrinterAssignments.AnyAsync(a =>
                a.PrinterId == printer.PrinterId &&
                (a.Status == "SCHEDULED" || a.Status == "PRINTING") &&
                a.AssignedStart <= now && a.AssignedEnd > now);
            printer.Status = busy ? "BUSY" : "AVAILABLE";
        }

        await _db.SaveChangesAsync();
    }

    private IActionResult ErrorRedirect(int id, string message)
    {
        TempData["Error"] = message;
        return RedirectToPage(new { id });
    }

    public class MaintenanceRow
    {
        public int MaintenanceId { get; set; }
        public string Type { get; set; } = "";
        public string? Description { get; set; }
        public string TechnicianName { get; set; } = "";
        public decimal Cost { get; set; }
        public string Status { get; set; } = "";
    }
}
