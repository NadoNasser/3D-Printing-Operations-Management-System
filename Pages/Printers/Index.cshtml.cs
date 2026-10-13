using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Common;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Printers;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;
    public List<Printer> Printers { get; set; } = new();
    public string? StatusFilter { get; set; }
    public string? SuggestedFor { get; set; }
    public List<PrinterSuggestion> Suggestions { get; set; } = new();

    public async Task OnGetAsync(string? material, string? statusFilter)
    {
        await SynchronizePrinterStatusesAsync();
        var query = _db.Printers.AsQueryable();
        StatusFilter = string.IsNullOrWhiteSpace(statusFilter) ? null : statusFilter.Trim().ToUpperInvariant();
        if (StatusFilter != null && DomainConstants.PrinterStatuses.Contains(StatusFilter)) query = query.Where(p => p.Status == StatusFilter);
        Printers = await query.OrderBy(p => p.PrinterCode).ToListAsync();

        if (!string.IsNullOrWhiteSpace(material))
        {
            SuggestedFor = material.Trim().ToUpperInvariant();
            Suggestions = await _db.Database.SqlQueryRaw<PrinterSuggestion>("EXEC dbo.usp_SuggestPrinter @Material = {0}", SuggestedFor).ToListAsync();
        }
    }

    public async Task<IActionResult> OnPostAsync(int id, string status, string? currentFilter)
    {
        status = (status ?? "").Trim().ToUpperInvariant();
        if (!DomainConstants.PrinterStatuses.Contains(status)) return ErrorRedirect(currentFilter, "That printer status is not valid.");
        if (status == "BUSY") return ErrorRedirect(currentFilter, "BUSY is managed automatically from the active printer schedule.");

        var printer = await _db.Printers.FindAsync(id);
        if (printer == null) return NotFound();
        var hasActiveAssignment = await _db.PrinterAssignments.AnyAsync(a => a.PrinterId == id && (a.Status == "SCHEDULED" || a.Status == "PRINTING") && a.AssignedEnd > DateTime.Now);
        var hasActiveMaintenance = await _db.AssetMaintenances.AnyAsync(m => m.AssetId == printer.AssetId && m.Status == "IN_PROGRESS");
        if (hasActiveMaintenance && status != "MAINTENANCE")
            return ErrorRedirect(currentFilter, $"Printer {printer.PrinterCode} has active maintenance and must remain in MAINTENANCE status until it is completed.");
        if (hasActiveAssignment && (status is "MAINTENANCE" or "OUT_OF_SERVICE" or "INACTIVE"))
            return ErrorRedirect(currentFilter, $"Printer {printer.PrinterCode} has an active scheduled/printing job and cannot be changed to {status}.");

        printer.Status = status;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Printer {printer.PrinterCode} status updated to {status}.";
        return RedirectToPage(new { statusFilter = currentFilter });
    }

    public string BadgeClass(string status) => WorkflowRules.StatusBadgeClass(status);
    public IEnumerable<string> ManualStatuses => DomainConstants.PrinterStatuses.Where(s => s != "BUSY");

    public List<PrinterMaterialRating> ParsedRatings(Printer p)
    {
        if (string.IsNullOrWhiteSpace(p.BestPrintMaterial)) return new();
        try
        {
            var ratings = JsonSerializer.Deserialize<List<PrinterMaterialRating>>(p.BestPrintMaterial, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return ratings?.OrderByDescending(r => r.Rating).ToList() ?? new();
        }
        catch (JsonException) { return new(); }
    }

    private IActionResult ErrorRedirect(string? filter, string message)
    {
        TempData["Error"] = message;
        return RedirectToPage(new { statusFilter = filter });
    }

    private Task SynchronizePrinterStatusesAsync() => PrinterStatusSync.SynchronizeAsync(_db);
}
