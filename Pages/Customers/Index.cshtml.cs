using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Customers;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public string? Query { get; set; }
    public List<Customer> CustomersList { get; set; } = new();

    public async Task OnGetAsync(string? q)
    {
        Query = q;
        var query = _db.Customers.Include(c => c.Addresses).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(c => c.Name.Contains(q) || c.CustomerCode.Contains(q) || (c.Phone != null && c.Phone.Contains(q)));
        CustomersList = await query.OrderBy(c => c.Name).ToListAsync();
    }
}
