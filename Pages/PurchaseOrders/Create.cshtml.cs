using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.PurchaseOrders;

[Authorize(Policy = "AdminOnly")]
public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    public CreateModel(AppDbContext db) => _db = db;

    [BindProperty, Required] public string PONumber { get; set; } = "";
    [BindProperty] public int SupplierId { get; set; }
    [BindProperty] public DateTime? ExpectedDeliveryDate { get; set; }
    [BindProperty] public List<LineInput> Items { get; set; } = new() { new LineInput() };

    public List<Supplier> SuppliersList { get; set; } = new();
    public List<Material> MaterialsList { get; set; } = new();

    public class LineInput
    {
        public int MaterialId { get; set; }
        public decimal QuantityOrdered { get; set; }
        public decimal UnitCost { get; set; }
    }

    private async Task LoadListsAsync()
    {
        SuppliersList = await _db.Suppliers.OrderBy(s => s.Name).ToListAsync();
        MaterialsList = await _db.Materials.OrderBy(m => m.MaterialName).ToListAsync();
    }

    public async Task OnGetAsync() => await LoadListsAsync();

    public async Task<IActionResult> OnPostAddLineAsync()
    {
        Items.Add(new LineInput());
        await LoadListsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var validItems = Items.Where(i => i.MaterialId > 0 && i.QuantityOrdered > 0).ToList();
        if (!validItems.Any())
        {
            ModelState.AddModelError(string.Empty, "Add at least one valid item (material and quantity greater than zero).");
            await LoadListsAsync();
            return Page();
        }

        var po = new PurchaseOrder
        {
            PONumber = PONumber,
            SupplierId = SupplierId,
            OrderDate = DateTime.Now,
            ExpectedDeliveryDate = ExpectedDeliveryDate,
            Status = "DRAFT"
        };
        foreach (var i in validItems)
            po.Items.Add(new PurchaseItem { MaterialId = i.MaterialId, QuantityOrdered = i.QuantityOrdered, UnitCost = i.UnitCost });

        _db.PurchaseOrders.Add(po);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (Microsoft.Data.SqlClient.SqlException)
        {
            ModelState.AddModelError(string.Empty, "Could not save: the PO number may already be in use, or a quantity was invalid.");
            await LoadListsAsync();
            return Page();
        }
        TempData["Success"] = "Purchase order created.";
        return RedirectToPage("Details", new { id = po.PurchaseOrderId });
    }
}
