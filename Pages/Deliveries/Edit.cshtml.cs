using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Common;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Deliveries;

public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    public EditModel(AppDbContext db) => _db = db;
    [BindProperty] public Delivery Delivery { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var d = await _db.Deliveries.FindAsync(id); if (d == null) return NotFound(); Delivery = d; return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var existing = await _db.Deliveries.FindAsync(Delivery.DeliveryId); if (existing == null) return NotFound();
        if (!DomainConstants.DeliveryStatuses.Contains(Delivery.Status)) { ModelState.AddModelError(string.Empty, "Invalid delivery status."); return Page(); }
        if (Delivery.Status == "DELIVERED") existing.DeliveredDate ??= DateTime.Now;
        existing.Status = Delivery.Status; existing.TrackingNumber = Delivery.TrackingNumber; existing.DeliveredDate = Delivery.DeliveredDate;
        await _db.SaveChangesAsync();
        if (existing.Status == "DELIVERED")
        {
            var order = await _db.Orders.FindAsync(existing.OrderId);
            if (order != null && order.Status == "READY_FOR_DELIVERY") { order.Status = "DELIVERED"; await _db.SaveChangesAsync(); }
        }
        var completed = existing.Status == "DELIVERED" && await OrderAutoStatus.TryCompleteAsync(_db, existing.OrderId, User);
        TempData["Success"] = completed ? "Delivery updated. The order is delivered and fully paid, so it is now COMPLETED." : "Delivery updated."; return RedirectToPage("/Orders/Details", new { id = existing.OrderId });
    }
}
