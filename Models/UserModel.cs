using System.ComponentModel.DataAnnotations;

namespace PrintShop3D.Models;

// NOTE: This table does NOT exist in create_database.sql. It is a small,
// additive table required for login (the original schema has no login/password
// data anywhere). Create it with db/002_add_users_table.sql. No existing
// table is modified - see README "Database changes" section.
public class AppUser
{
    public int UserId { get; set; }
    [Required, StringLength(50)] public string Username { get; set; } = "";
    [Required] public string PasswordHash { get; set; } = "";
    [Required, StringLength(20)] public string Role { get; set; } = "Staff"; // Admin | Staff
    public int? EmployeeId { get; set; } // optional link to EMPLOYEES
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public Employee? Employee { get; set; }
}
