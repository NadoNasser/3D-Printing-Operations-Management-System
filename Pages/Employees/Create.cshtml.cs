using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Employees;

[Authorize(Policy = "AdminOnly")]
public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    public CreateModel(AppDbContext db) => _db = db;

    [BindProperty] public Employee Employee { get; set; } = new() { HireDate = DateTime.Today, Status = "ACTIVE" };

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        _db.Employees.Add(Employee);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Employee created.";
        return RedirectToPage("Index");
    }
}
