using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using PrintShop3D.Common;
using PrintShop3D.Data;
using PrintShop3D.Models;
using System.Security.Claims;

namespace PrintShop3D.Pages.ProductionJobs;

public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    public EditModel(AppDbContext db) => _db = db;

    public ProductionJob Job { get; set; } = new();
    public List<Printer> AvailablePrinters { get; set; } = new();
    public List<Employee> ActiveEmployees { get; set; } = new();
    public IEnumerable<string> AllowedStatuses => DomainConstants.ProductionJobStatuses.Where(s => WorkflowRules.IsValidProductionTransition(Job.Status, s));

    public async Task<IActionResult> OnGetAsync(int id)
    {
        await SynchronizePrinterStatusesAsync();
        var job = await LoadJobAsync(id);
        if (job == null) return NotFound();
        Job = job;
        AvailablePrinters = await _db.Printers.Where(p => p.Status == "AVAILABLE").OrderBy(p => p.PrinterCode).ToListAsync();
        ActiveEmployees = await _db.Employees.Where(e => e.Status == "ACTIVE").OrderBy(e => e.FullName).ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostPriorityAsync(int id, int priority)
    {
        var job = await _db.ProductionJobs.FirstOrDefaultAsync(j => j.JobId == id);
        if (job == null) return NotFound();
        if (!DomainConstants.ProductionJobPriorities.Contains(priority))
            return ErrorRedirect(id, "That production priority is not valid.");

        job.Priority = priority;
        job.LastUpdatedAt = DateTime.Now;
        try
        {
            await SaveWithEmployeeContextAsync();
            TempData["Success"] = $"Job #{job.JobId} priority updated to {PriorityLabel(priority)}.";
        }
        catch (SqlException ex) { TempData["Error"] = FriendlySqlError(ex); }
        return RedirectToPage("Edit", new { id });
    }

    private static string PriorityLabel(int priority) => priority switch
    {
        4 => "URGENT",
        3 => "HIGH",
        2 => "NORMAL",
        _ => "LOW"
    };

    public async Task<IActionResult> OnPostStatusAsync(int id, string status)
    {
        status = (status ?? "").Trim().ToUpperInvariant();
        var job = await _db.ProductionJobs
            .Include(j => j.OrderItem)
            .Include(j => j.PrinterAssignments)
            .Include(j => j.JobAssignments)
            .FirstOrDefaultAsync(j => j.JobId == id);
        if (job == null) return NotFound();

        if (!DomainConstants.ProductionJobStatuses.Contains(status))
            return ErrorRedirect(id, "That production status is not valid.");
        if (!WorkflowRules.IsValidProductionTransition(job.Status, status))
            return ErrorRedirect(id, $"A production job cannot move directly from {job.Status} to {status}.");
        if (status == "IN_PROGRESS" && !HasActivePrinter(job))
            return ErrorRedirect(id, "Assign a printer with an active schedule before starting the job.");
        if (status is "QUALITY_CHECK" or "COMPLETED" && !HasPrimaryEmployee(job))
            return ErrorRedirect(id, "Assign a primary employee before moving the job beyond production.");

        var oldStatus = job.Status;
        job.Status = status;
        job.LastUpdatedAt = DateTime.Now;

        if (status == "IN_PROGRESS")
        {
            job.StartTime ??= DateTime.Now;
            foreach (var assignment in job.PrinterAssignments.Where(a => a.Status == "SCHEDULED"))
            {
                assignment.Status = "PRINTING";
                assignment.ActualStart ??= DateTime.Now;
            }
            foreach (var assignment in job.JobAssignments.Where(a => a.Status == "ASSIGNED"))
                assignment.Status = "IN_PROGRESS";
        }
        else if (status is "COMPLETED" or "FAILED")
        {
            job.EndTime = DateTime.Now;
            if (job.StartTime.HasValue)
                job.ActualTime = (decimal)Math.Max(0, (job.EndTime.Value - job.StartTime.Value).TotalHours);

            foreach (var assignment in job.PrinterAssignments.Where(a => a.Status == "SCHEDULED" || a.Status == "PRINTING"))
            {
                assignment.Status = status == "COMPLETED" ? "COMPLETED" : "FAILED";
                assignment.ActualEnd ??= DateTime.Now;
            }
            foreach (var assignment in job.JobAssignments.Where(a => a.Status is "ASSIGNED" or "IN_PROGRESS"))
                assignment.Status = status == "COMPLETED" ? "COMPLETED" : "REASSIGNED";
        }

        try
        {
            await SaveWithEmployeeContextAsync();
            await UpdateOrderFromProductionAsync(job.OrderItemId);
            await SynchronizePrinterStatusesAsync();
            TempData["Success"] = $"Job #{job.JobId} status changed from {oldStatus} to {status}.";
        }
        catch (SqlException ex)
        {
            TempData["Error"] = FriendlySqlError(ex);
        }
        return RedirectToPage("Edit", new { id });
    }

    public async Task<IActionResult> OnPostAssignPrinterAsync(int jobId, int printerId, DateTime start, DateTime end)
    {
        var job = await _db.ProductionJobs.FirstOrDefaultAsync(j => j.JobId == jobId);
        if (job == null) return NotFound();
        if (start >= end) return AssignmentError(jobId, "The printer end time must be after the start time.");
        if (start < DateTime.Now.AddMinutes(-1)) return AssignmentError(jobId, "A printer assignment cannot start in the past.");

        var printer = await _db.Printers.FindAsync(printerId);
        if (printer == null) return AssignmentError(jobId, "The selected printer no longer exists.");
        if (printer.Status is "MAINTENANCE" or "OUT_OF_SERVICE" or "INACTIVE")
            return AssignmentError(jobId, $"Printer {printer.PrinterCode} is {printer.Status} and cannot be scheduled.");

        var overlaps = await _db.PrinterAssignments.AnyAsync(a =>
            a.PrinterId == printerId &&
            (a.Status == "SCHEDULED" || a.Status == "PRINTING") &&
            a.AssignedStart < end && a.AssignedEnd > start);
        if (overlaps) return AssignmentError(jobId, "That printer is already booked for an overlapping time. Please choose another time or printer.");

        _db.PrinterAssignments.Add(new PrinterAssignment
        {
            JobId = jobId,
            PrinterId = printerId,
            AssignedStart = start,
            AssignedEnd = end,
            Status = start <= DateTime.Now && end > DateTime.Now ? "PRINTING" : "SCHEDULED",
            ActualStart = start <= DateTime.Now && end > DateTime.Now ? DateTime.Now : null
        });

        job.LastUpdatedAt = DateTime.Now;
        try
        {
            await SaveWithEmployeeContextAsync();
            await SynchronizePrinterStatusesAsync();
            TempData["Success"] = $"Printer {printer.PrinterCode} assigned to job #{jobId}.";
        }
        catch (SqlException ex) { TempData["Error"] = FriendlySqlError(ex); }
        return RedirectToPage("Edit", new { id = jobId });
    }

    public async Task<IActionResult> OnPostAssignEmployeeAsync(int jobId, int employeeId, DateTime start, DateTime end)
    {
        var job = await _db.ProductionJobs.FindAsync(jobId);
        if (job == null) return NotFound();
        if (start >= end) return AssignmentError(jobId, "The employee assignment end time must be after the start time.");
        if (start < DateTime.Now.AddMinutes(-1)) return AssignmentError(jobId, "An employee assignment cannot start in the past.");

        var employee = await _db.Employees.FindAsync(employeeId);
        if (employee == null || employee.Status != "ACTIVE") return AssignmentError(jobId, "The selected employee is not active.");

        var overlaps = await _db.JobAssignments.AnyAsync(a =>
            a.EmployeeId == employeeId &&
            (a.Status == "ASSIGNED" || a.Status == "IN_PROGRESS") &&
            a.AssignedStart < end && a.AssignedEnd > start);
        if (overlaps) return AssignmentError(jobId, $"{employee.FullName} already has another active assignment during that time.");

        _db.JobAssignments.Add(new JobAssignment
        {
            JobId = jobId, EmployeeId = employeeId, Role = "PRIMARY",
            AssignedStart = start, AssignedEnd = end, Status = "ASSIGNED"
        });
        job.LastUpdatedAt = DateTime.Now;

        try
        {
            await SaveWithEmployeeContextAsync();
            TempData["Success"] = $"{employee.FullName} assigned to job #{jobId}.";
        }
        catch (SqlException ex) { TempData["Error"] = FriendlySqlError(ex); }
        return RedirectToPage("Edit", new { id = jobId });
    }

    private async Task<ProductionJob?> LoadJobAsync(int id) => await _db.ProductionJobs
        .Include(j => j.OrderItem).ThenInclude(oi => oi!.Product)
        .Include(j => j.OrderItem).ThenInclude(oi => oi!.Service)
        .Include(j => j.PrinterAssignments).ThenInclude(pa => pa.Printer)
        .Include(j => j.JobAssignments).ThenInclude(ja => ja.Employee)
        .FirstOrDefaultAsync(j => j.JobId == id);

    private static bool HasActivePrinter(ProductionJob job) => job.PrinterAssignments.Any(a => a.Status is "SCHEDULED" or "PRINTING");
    private static bool HasPrimaryEmployee(ProductionJob job) => job.JobAssignments.Any(a => a.Role == "PRIMARY" && (a.Status is "ASSIGNED" or "IN_PROGRESS" or "COMPLETED"));

    private IActionResult AssignmentError(int jobId, string message) => ErrorRedirect(jobId, message);
    private IActionResult ErrorRedirect(int id, string message)
    {
        TempData["Error"] = message;
        return RedirectToPage("Edit", new { id });
    }

    private async Task SaveWithEmployeeContextAsync()
    {
        var employeeId = await ResolveCurrentEmployeeIdAsync();
        await _db.Database.OpenConnectionAsync();
        try
        {
            if (employeeId.HasValue)
                await _db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'CurrentEmployeeID', @value={employeeId.Value}");
            await _db.SaveChangesAsync();
        }
        finally { await _db.Database.CloseConnectionAsync(); }
    }

    private async Task<int?> ResolveCurrentEmployeeIdAsync()
    {
        var username = User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(username)) return null;
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username && u.IsActive);
        return user?.EmployeeId;
    }

    private Task UpdateOrderFromProductionAsync(int orderItemId) => Task.CompletedTask;

    private Task SynchronizePrinterStatusesAsync() => PrinterStatusSync.SynchronizeAsync(_db);

    private static string FriendlySqlError(SqlException ex)
    {
        if (ex.Message.Contains("overlap", StringComparison.OrdinalIgnoreCase)) return "The selected schedule overlaps another active assignment. Choose another time or resource.";
        if (ex.Message.Contains("trigger", StringComparison.OrdinalIgnoreCase) && ex.Message.Contains("OUTPUT", StringComparison.OrdinalIgnoreCase)) return "The database trigger configuration is incomplete. Run db/007_system_integrity_improvements.sql and try again.";
        return "The change could not be saved. Please check the selected resource and schedule.";
    }
}
