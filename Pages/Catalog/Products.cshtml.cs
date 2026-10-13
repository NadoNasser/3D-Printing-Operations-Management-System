using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Catalog;

public class ProductsModel : PageModel
{
    private readonly AppDbContext _db;
    public ProductsModel(AppDbContext db) => _db = db;

    public List<Product> ProductsList { get; set; } = new();
    public int? EditingId { get; set; }
    public string? Search { get; set; }
    [BindProperty] public Product Form { get; set; } = new() { Unit = "PCS" };

    // Search matches name, category, or description (case-insensitive, handled by the database collation).
    private async Task LoadProductsAsync(string? search)
    {
        Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var query = _db.Products.AsQueryable();
        if (Search != null)
            query = query.Where(p => p.Name.Contains(Search) ||
                                     (p.Category != null && p.Category.Contains(Search)) ||
                                     (p.Description != null && p.Description.Contains(Search)));
        ProductsList = await query.OrderBy(p => p.Name).ToListAsync();
    }

    public async Task OnGetAsync(string? search) => await LoadProductsAsync(search);

    public async Task<IActionResult> OnGetEditAsync(int id, string? search)
    {
        var p = await _db.Products.FindAsync(id);
        if (p == null) return NotFound();
        Form = p; EditingId = id;
        await LoadProductsAsync(search);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? search)
    {
        if (Form.ProductId == 0)
        {
            _db.Products.Add(Form);
        }
        else
        {
            var existing = await _db.Products.FindAsync(Form.ProductId);
            if (existing == null) return NotFound();
            existing.Name = Form.Name; existing.Category = Form.Category;
            existing.Description = Form.Description; existing.BasePrice = Form.BasePrice; existing.Unit = Form.Unit;
        }
        await _db.SaveChangesAsync();
        TempData["Success"] = "Product saved.";
        return RedirectToPage("Products", new { search });
    }
}
