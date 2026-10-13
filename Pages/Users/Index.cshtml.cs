using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Common;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Users;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<AppUser> _hasher;
    public IndexModel(AppDbContext db, IPasswordHasher<AppUser> hasher) { _db = db; _hasher = hasher; }
    public List<AppUser> Users { get; set; } = new();
    public List<Employee> Employees { get; set; } = new();
    [BindProperty] public NewUserInput NewUser { get; set; } = new();
    [BindProperty] public EditUserInput EditUser { get; set; } = new();

    public class NewUserInput
    {
        [Required, StringLength(50)] public string Username { get; set; } = "";
        [Required, StringLength(100, MinimumLength = 8)] public string Password { get; set; } = "";
        [Required] public string Role { get; set; } = "Staff";
        public int? EmployeeId { get; set; }
    }
    public class EditUserInput
    {
        public int UserId { get; set; }
        [Required] public string Role { get; set; } = "Staff";
        public int? EmployeeId { get; set; }
        public bool IsActive { get; set; }
        [StringLength(100, MinimumLength = 8)] public string? NewPassword { get; set; }
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!DomainConstants.UserRoles.Contains(NewUser.Role)) ModelState.AddModelError(string.Empty, "Invalid role.");
        if (await _db.Users.AnyAsync(u => u.Username == NewUser.Username.Trim())) ModelState.AddModelError(string.Empty, "That username already exists.");
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
        var user = new AppUser { Username = NewUser.Username.Trim(), Role = NewUser.Role, EmployeeId = NewUser.EmployeeId, IsActive = true, CreatedAt = DateTime.Now };
        user.PasswordHash = _hasher.HashPassword(user, NewUser.Password);
        _db.Users.Add(user); await _db.SaveChangesAsync(); TempData["Success"] = $"User {user.Username} created."; return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditAsync()
    {
        var user = await _db.Users.FindAsync(EditUser.UserId); if (user == null) return NotFound();
        if (!DomainConstants.UserRoles.Contains(EditUser.Role)) { TempData["Error"] = "Invalid role."; return RedirectToPage(); }
        if (user.UserId == CurrentUserId() && !EditUser.IsActive) { TempData["Error"] = "You cannot deactivate your own account."; return RedirectToPage(); }
        user.Role = EditUser.Role; user.EmployeeId = EditUser.EmployeeId; user.IsActive = EditUser.IsActive;
        if (!string.IsNullOrWhiteSpace(EditUser.NewPassword)) user.PasswordHash = _hasher.HashPassword(user, EditUser.NewPassword);
        await _db.SaveChangesAsync(); TempData["Success"] = $"User {user.Username} updated."; return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Users = await _db.Users.Include(u => u.Employee).OrderBy(u => u.Username).ToListAsync();
        Employees = await _db.Employees.Where(e => e.Status == "ACTIVE").OrderBy(e => e.FullName).ToListAsync();
    }
    private int CurrentUserId() => int.TryParse(User.FindFirst("UserId")?.Value, out var id) ? id : 0;
}
