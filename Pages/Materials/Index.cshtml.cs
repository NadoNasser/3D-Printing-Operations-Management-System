using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Materials;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;
    public List<MaterialStockOnHand> Stock { get; set; } = new();
    public string? Search { get; set; }
    public bool LowOnly { get; set; }

    public async Task OnGetAsync(string? search, bool lowOnly = false)
    {
        Search = search?.Trim(); LowOnly = lowOnly;
        var query = _db.MaterialStockOnHand.AsQueryable();
        if (!string.IsNullOrWhiteSpace(Search)) query = query.Where(m => m.MaterialName.Contains(Search) || m.SKU.Contains(Search));
        if (LowOnly) query = query.Where(m => m.NeedsReorder == 1);
        Stock = await query.OrderBy(m => m.MaterialName).ToListAsync();
    }
}
