using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.PurchaseOrders;

[Authorize(Policy = "AdminOnly")]
public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;
    public DetailsModel(AppDbContext db) => _db = db;

    public PurchaseOrder Po { get; set; } = new();

    public class ReceiptLine
    {
        public int PurchaseItemId { get; set; }
        public decimal QuantityReceived { get; set; }
        public decimal UnitCost { get; set; }
    }
    [BindProperty] public List<ReceiptLine> Lines { get; set; } = new();
    [BindProperty] public int PurchaseOrderId { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var po = await _db.PurchaseOrders.Include(p => p.Supplier).Include(p => p.Items).ThenInclude(i => i.Material)
            .FirstOrDefaultAsync(p => p.PurchaseOrderId == id);
        if (po == null) return NotFound();

        var receivedByItem = await _db.GoodsReceiptItems
            .Where(gri => po.Items.Select(i => i.PurchaseItemId).Contains(gri.PurchaseItemId))
            .GroupBy(gri => gri.PurchaseItemId)
            .Select(g => new { PurchaseItemId = g.Key, Total = g.Sum(x => x.QuantityReceived) })
            .ToListAsync();
        foreach (var item in po.Items)
            item.QuantityReceivedSoFar = receivedByItem.FirstOrDefault(r => r.PurchaseItemId == item.PurchaseItemId)?.Total ?? 0;

        Po = po;
        return Page();
    }

    public async Task<IActionResult> OnPostStatusAsync(int id, string status)
    {
        var po = await _db.PurchaseOrders.FindAsync(id);
        if (po == null) return NotFound();
        po.Status = status;
        await _db.SaveChangesAsync();
        return RedirectToPage("Details", new { id });
    }

    public async Task<IActionResult> OnPostReceiveAsync()
    {
        var validLines = Lines.Where(l => l.QuantityReceived > 0).ToList();
        if (!validLines.Any())
        {
            TempData["Error"] = "Enter a quantity received for at least one line.";
            return RedirectToPage("Details", new { id = PurchaseOrderId });
        }

        var receivedBy = await ResolveCurrentEmployeeIdAsync();
        if (receivedBy == null)
        {
            TempData["Error"] = "Your login is not linked to an Employee record, so a receipt can't be attributed to you. " +
                                 "Ask an admin to set USERS.EmployeeID for your account.";
            return RedirectToPage("Details", new { id = PurchaseOrderId });
        }

        var receipt = new GoodsReceipt { PurchaseOrderId = PurchaseOrderId, ReceiptDate = DateTime.Now, ReceivedBy = receivedBy.Value };
        foreach (var l in validLines)
            receipt.Items.Add(new GoodsReceiptItem { PurchaseItemId = l.PurchaseItemId, QuantityReceived = l.QuantityReceived, UnitCost = l.UnitCost });
        _db.GoodsReceipts.Add(receipt);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (Microsoft.Data.SqlClient.SqlException)
        {
            TempData["Error"] = "The receipt could not be saved. Please check the quantities entered.";
            return RedirectToPage("Details", new { id = PurchaseOrderId });
        }

        TempData["Success"] = "Goods receipt recorded and stock updated.";
        return RedirectToPage("Details", new { id = PurchaseOrderId });
    }

    private async Task<int?> ResolveCurrentEmployeeIdAsync()
    {
        var username = User.Identity?.Name;
        if (username == null) return null;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        return user?.EmployeeId;
    }
}
