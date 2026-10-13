using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Employees;

// Kept in sync with _Layout.cshtml, which only shows the Employees link to Admins.
[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;
    public List<Employee> EmployeesList { get; set; } = new();
    public async Task OnGetAsync() => EmployeesList = await _db.Employees.OrderBy(e => e.FullName).ToListAsync();
}
