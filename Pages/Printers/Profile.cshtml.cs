using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Printers;

public class ProfileModel : PageModel
{
    private readonly AppDbContext _db;
    public ProfileModel(AppDbContext db) => _db = db;

    public Printer Printer { get; set; } = new();
    public int MaintenanceCount { get; set; }
    public decimal TotalMaintenanceCost { get; set; }
    public AssetMaintenance? LatestMaintenance { get; set; }
    [BindProperty] public int PrinterId { get; set; }
    [BindProperty] public string? PrintQuality { get; set; }
    [BindProperty] public List<PrinterMaterialRating> Ratings { get; set; } = new() { new PrinterMaterialRating { Rating = 5 } };

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var p = await _db.Printers.FindAsync(id);
        if (p == null) return NotFound();
        Printer = p;
        PrinterId = p.PrinterId;
        PrintQuality = p.PrintQuality;
        await LoadMaintenanceSummaryAsync(p.AssetId);
        if (!string.IsNullOrWhiteSpace(p.BestPrintMaterial))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<PrinterMaterialRating>>(
                    p.BestPrintMaterial, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed is { Count: > 0 }) Ratings = parsed;
            }
            catch (JsonException) { /* fall back to the default single empty row */ }
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAddRowAsync(int id)
    {
        Ratings.Add(new PrinterMaterialRating { Rating = 5 });
        var p = await _db.Printers.FindAsync(id);
        if (p == null) return NotFound();
        Printer = p;
        await LoadMaintenanceSummaryAsync(p.AssetId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var printer = await _db.Printers.FindAsync(PrinterId);
        if (printer == null) return NotFound();

        var validRatings = Ratings.Where(r => !string.IsNullOrWhiteSpace(r.Material) && r.Rating is >= 1 and <= 5).ToList();
        if (validRatings.GroupBy(r => r.Material).Any(g => g.Count() > 1))
        {
            ModelState.AddModelError(string.Empty, "Each material should appear only once in a printer's profile.");
            Printer = printer;
            await LoadMaintenanceSummaryAsync(printer.AssetId);
            return Page();
        }

        printer.PrintQuality = string.IsNullOrWhiteSpace(PrintQuality) ? null : PrintQuality;
        // IMPORTANT: dbo.usp_SuggestPrinter reads this JSON with OPENJSON ... WITH
        // (material '$.material', rating '$.rating') which is case-sensitive, so
        // the keys MUST be lowercase "material"/"rating", not the C# property
        // names "Material"/"Rating". CamelCase naming policy gives us that.
        printer.BestPrintMaterial = validRatings.Any()
            ? JsonSerializer.Serialize(validRatings, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            : null;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (Microsoft.Data.SqlClient.SqlException)
        {
            ModelState.AddModelError(string.Empty, "Could not save this profile - the database rejected the saved JSON.");
            Printer = printer;
            await LoadMaintenanceSummaryAsync(printer.AssetId);
            return Page();
        }

        TempData["Success"] = "Printer profile saved.";
        return RedirectToPage("Index");
    }

    private async Task LoadMaintenanceSummaryAsync(int assetId)
    {
        MaintenanceCount = await _db.AssetMaintenances.CountAsync(m => m.AssetId == assetId);
        TotalMaintenanceCost = await _db.AssetMaintenances
            .Where(m => m.AssetId == assetId)
            .Select(m => (decimal?)m.Cost)
            .SumAsync() ?? 0m;
        LatestMaintenance = await _db.AssetMaintenances
            .Where(m => m.AssetId == assetId)
            .OrderByDescending(m => m.MaintenanceId)
            .FirstOrDefaultAsync();
    }
}
