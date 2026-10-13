using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Orders;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;
    public List<Order> OrdersList { get; set; } = new();
    public string? Search { get; set; }
    public string? StatusFilter { get; set; }

    public async Task OnGetAsync(string? search, string? statusFilter)
    {
        Search = search?.Trim();
        StatusFilter = string.IsNullOrWhiteSpace(statusFilter) ? null : statusFilter.Trim().ToUpperInvariant();
        var query = _db.Orders.Include(o => o.Customer).Include(o => o.Items).Include(o => o.Payments).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Search))
        {
            if (int.TryParse(Search, out var orderId)) query = query.Where(o => o.OrderId == orderId || o.Customer!.Name.Contains(Search));
            else query = query.Where(o => o.Customer!.Name.Contains(Search));
        }
        if (!string.IsNullOrWhiteSpace(StatusFilter))
        {
            if (StatusFilter == "OPEN")
            {
                var closedStatuses = new[] { "COMPLETED", "CANCELED", "DELIVERED" };
                query = query.Where(o => !closedStatuses.Contains(o.Status));
            }
            else
            {
                query = query.Where(o => o.Status == StatusFilter);
            }
        }
        OrdersList = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
    }
}
