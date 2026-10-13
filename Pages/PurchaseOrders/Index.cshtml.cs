using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.PurchaseOrders;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;
    public List<PurchaseOrder> Orders { get; set; } = new();
    public async Task OnGetAsync() =>
        Orders = await _db.PurchaseOrders.Include(p => p.Supplier).OrderByDescending(p => p.OrderDate).ToListAsync();
}
