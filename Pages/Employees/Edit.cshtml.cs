using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Employees;

[Authorize(Policy = "AdminOnly")]
public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    public EditModel(AppDbContext db) => _db = db;

    [BindProperty] public Employee Employee { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var e = await _db.Employees.FindAsync(id);
        if (e == null) return NotFound();
        Employee = e;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var existing = await _db.Employees.FindAsync(Employee.EmployeeId);
        if (existing == null) return NotFound();
        existing.EmployeeCode = Employee.EmployeeCode;
        existing.FullName = Employee.FullName;
        existing.PrimaryRole = Employee.PrimaryRole;
        existing.Status = Employee.Status;
        existing.Phone = Employee.Phone;
        existing.Email = Employee.Email;
        existing.HourlyRate = Employee.HourlyRate;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Employee updated.";
        return RedirectToPage("Index");
    }
}
