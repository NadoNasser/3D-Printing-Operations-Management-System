using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Materials;

public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    public CreateModel(AppDbContext db) => _db = db;
    [BindProperty] public Material Material { get; set; } = new() { Unit = "KG" };
    public void OnGet() { }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        _db.Materials.Add(Material);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Material created.";
        return RedirectToPage("Index");
    }
}
