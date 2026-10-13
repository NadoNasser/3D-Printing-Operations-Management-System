using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Common;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Deliveries;

public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    public CreateModel(AppDbContext db) => _db = db;
    [BindProperty] public int OrderId { get; set; }
    [BindProperty] public Delivery Delivery { get; set; } = new() { Method = "PICKUP", Status = "PENDING" };
    public List<CustomerAddress> Addresses { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int orderId)
    {
        var order = await _db.Orders.FindAsync(orderId); if (order == null) return NotFound();
        OrderId = orderId; Addresses = await _db.CustomerAddresses.Where(a => a.CustomerId == order.CustomerId).OrderByDescending(a => a.IsDefault).ThenBy(a => a.Label).ToListAsync(); return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var order = await _db.Orders.FindAsync(OrderId); if (order == null) return NotFound();
        if (!DomainConstants.DeliveryMethods.Contains(Delivery.Method) || !DomainConstants.DeliveryStatuses.Contains(Delivery.Status)) ModelState.AddModelError(string.Empty, "Invalid delivery method or status.");
        if (Delivery.Method != "PICKUP" && Delivery.AddressId == null) ModelState.AddModelError(string.Empty, "Choose a customer address for delivery or shipping.");
        if (!ModelState.IsValid) { Addresses = await _db.CustomerAddresses.Where(a => a.CustomerId == order.CustomerId).OrderByDescending(a => a.IsDefault).ToListAsync(); return Page(); }
        Delivery.OrderId = OrderId; _db.Deliveries.Add(Delivery); await _db.SaveChangesAsync();
        TempData["Success"] = "Delivery set up."; return RedirectToPage("/Orders/Details", new { id = OrderId });
    }
}
