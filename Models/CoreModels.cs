using System.ComponentModel.DataAnnotations;

namespace PrintShop3D.Models;

public class Customer
{
    public int CustomerId { get; set; }
    [Required, StringLength(50)] public string CustomerCode { get; set; } = "";
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [StringLength(25)] public string? Phone { get; set; }
    [StringLength(100)] public string? Email { get; set; }
    [StringLength(150)] public string? Company { get; set; }
    public string? Notes { get; set; }
    public bool IsBlacklisted { get; set; }
    [StringLength(500)] public string? BlacklistReason { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<CustomerAddress> Addresses { get; set; } = new();
}

public class CustomerAddress
{
    public int AddressId { get; set; }
    public int CustomerId { get; set; }
    [Required, StringLength(50)] public string Label { get; set; } = "";
    [Required, StringLength(255)] public string AddressLine { get; set; } = "";
    [Required, StringLength(100)] public string City { get; set; } = "";
    public bool IsDefault { get; set; }

    public Customer? Customer { get; set; }
}

public class Employee
{
    public int EmployeeId { get; set; }
    [Required, StringLength(50)] public string EmployeeCode { get; set; } = "";
    [Required, StringLength(150)] public string FullName { get; set; } = "";
    [Required, StringLength(100)] public string PrimaryRole { get; set; } = "";
    [StringLength(25)] public string? Phone { get; set; }
    [StringLength(100)] public string? Email { get; set; }
    public DateTime HireDate { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public decimal HourlyRate { get; set; }
    public string? Notes { get; set; }
}

public class Skill
{
    public int SkillId { get; set; }
    [Required, StringLength(100)] public string SkillName { get; set; } = "";
    public string? Description { get; set; }
}

public class EmployeeSkill
{
    public int EmployeeSkillId { get; set; }
    public int EmployeeId { get; set; }
    public int SkillId { get; set; }
    public string ProficiencyLevel { get; set; } = "BASIC";
}
