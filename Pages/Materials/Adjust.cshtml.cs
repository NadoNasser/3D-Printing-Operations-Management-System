using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Materials;

public class AdjustModel : PageModel
{
    private readonly AppDbContext _db;
    public AdjustModel(AppDbContext db) => _db = db;
    [BindProperty] public int MaterialId { get; set; }
    [BindProperty, Required] public decimal Quantity { get; set; }
    [BindProperty] public decimal UnitCost { get; set; }
    [BindProperty, Required] public string Reason { get; set; } = "";
    public string MaterialName { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal CurrentStock { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var m = await _db.Materials.FindAsync(id);
        if (m == null) return NotFound();
        MaterialId = id; Unit = m.Unit; MaterialName = m.MaterialName; UnitCost = m.PurchasePrice;
        CurrentStock = (await _db.MaterialStockOnHand.FirstOrDefaultAsync(s => s.MaterialId == id))?.CurrentStockLevel ?? 0;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var m = await _db.Materials.FindAsync(MaterialId);
        if (m == null) return NotFound();
        await LoadDisplayAsync(m);
        if (!ModelState.IsValid) return Page();
        if (Quantity == 0) { ModelState.AddModelError(string.Empty, "Quantity change cannot be zero."); return Page(); }
        if (UnitCost < 0) { ModelState.AddModelError(string.Empty, "Unit cost cannot be negative."); return Page(); }
        if (CurrentStock + Quantity < 0) { ModelState.AddModelError(string.Empty, $"This adjustment would make stock negative. Maximum removable quantity is {CurrentStock:0.###} {Unit}."); return Page(); }

        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            MaterialId = MaterialId, TransactionType = "ADJUSTMENT", Quantity = Quantity,
            UnitCost = UnitCost, TransactionDate = DateTime.Now, Reason = Reason.Trim()
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Stock adjustment recorded.";
        return RedirectToPage("Index");
    }

    private async Task LoadDisplayAsync(Material m)
    {
        MaterialName = m.MaterialName; Unit = m.Unit; CurrentStock = (await _db.MaterialStockOnHand.FirstOrDefaultAsync(s => s.MaterialId == MaterialId))?.CurrentStockLevel ?? 0;
    }
}
