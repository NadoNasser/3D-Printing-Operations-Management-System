using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Catalog;

public class ServicesModel : PageModel
{
    private readonly AppDbContext _db;
    public ServicesModel(AppDbContext db) => _db = db;

    public List<Service> ServicesList { get; set; } = new();
    public int? EditingId { get; set; }
    [BindProperty] public Service Form { get; set; } = new() { PricingUnit = "HOURS" };

    public async Task OnGetAsync() => ServicesList = await _db.Services.OrderBy(s => s.Name).ToListAsync();

    public async Task<IActionResult> OnGetEditAsync(int id)
    {
        var s = await _db.Services.FindAsync(id);
        if (s == null) return NotFound();
        Form = s; EditingId = id;
        ServicesList = await _db.Services.OrderBy(x => x.Name).ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Form.ServiceId == 0)
        {
            _db.Services.Add(Form);
        }
        else
        {
            var existing = await _db.Services.FindAsync(Form.ServiceId);
            if (existing == null) return NotFound();
            existing.Name = Form.Name; existing.Category = Form.Category;
            existing.Description = Form.Description; existing.BaseRate = Form.BaseRate; existing.PricingUnit = Form.PricingUnit;
        }
        await _db.SaveChangesAsync();
        TempData["Success"] = "Service saved.";
        return RedirectToPage("Services");
    }
}
