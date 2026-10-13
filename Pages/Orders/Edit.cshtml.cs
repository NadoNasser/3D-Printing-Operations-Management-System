using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Common;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Orders;

/// <summary>Edit an order. Allowed ONLY while the order status is DRAFT.</summary>
public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;
    public EditModel(AppDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    public Order Order { get; set; } = new();
    [BindProperty] public int CustomerId { get; set; }
    [BindProperty] public List<ExistingItemInput> Existing { get; set; } = new();
    [BindProperty] public List<OrderLineInput> Items { get; set; } = new();   // NEW lines only

    public List<Customer> CustomersList { get; set; } = new();
    public List<Product> ProductsList { get; set; } = new();
    public List<Service> ServicesList { get; set; } = new();

    public bool CanOverridePrice => User.IsInRole("Admin");
    public bool CanSetFilePrice => true;   // keep identical to Orders/Create

    // An item is locked once production has really started on it (anything beyond a bare QUEUED job).
    public bool IsLocked(OrderItem i) => i.ProductionJobs.Any(j =>
        j.Status != "QUEUED" || j.PrinterAssignments.Any() || j.JobAssignments.Any() || j.MaterialUsages.Any() || j.CostBreakdown != null);
    public bool IsPrintingItem(OrderItem i) => i.Service != null && ServiceRules.IsPrintingService(i.Service);
    public bool CanEditPrice(OrderItem i) => IsPrintingItem(i) && i.Files.Any() ? CanSetFilePrice : CanOverridePrice;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var order = await LoadOrderAsync(id);
        if (order == null) return NotFound();
        if (order.Status != "DRAFT") return ErrorRedirect(id, $"Only DRAFT orders can be edited. This order is {order.Status}.");

        Order = order;
        CustomerId = order.CustomerId;
        Existing = order.Items.Select(i => new ExistingItemInput { OrderItemId = i.OrderItemId, Quantity = i.Quantity, UnitPrice = i.UnitPrice }).ToList();
        await LoadListsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var order = await LoadOrderAsync(id);
        if (order == null) return NotFound();
        if (order.Status != "DRAFT") return ErrorRedirect(id, $"Only DRAFT orders can be edited. This order is {order.Status}.");

        if (CustomerId == 0) ModelState.AddModelError(string.Empty, "Please choose a customer.");
        else if (!await _db.Customers.AnyAsync(c => c.CustomerId == CustomerId))
            ModelState.AddModelError(string.Empty, "The selected customer no longer exists.");

        var employeeId = await ResolveCurrentEmployeeIdAsync();
        var removed = new List<OrderItem>();

        foreach (var input in Existing)
        {
            var item = order.Items.FirstOrDefault(i => i.OrderItemId == input.OrderItemId);
            if (item == null) { ModelState.AddModelError(string.Empty, "An item in the form does not belong to this order."); continue; }
            if (IsLocked(item)) continue;   // production already started: never modified here

            var label = item.Product?.Name ?? item.Service?.Name ?? $"Item {item.OrderItemId}";
            if (input.Remove) { removed.Add(item); continue; }
            if (input.Quantity < 1) { ModelState.AddModelError(string.Empty, $"{label}: quantity must be at least 1."); continue; }
            if (input.UnitPrice < 0) { ModelState.AddModelError(string.Empty, $"{label}: price cannot be negative."); continue; }

            if (CanEditPrice(item)) item.UnitPrice = input.UnitPrice;
            if (item.Quantity != input.Quantity)
            {
                item.Quantity = input.Quantity;
                foreach (var job in item.ProductionJobs) { job.Quantity = input.Quantity; job.LastUpdatedAt = DateTime.Now; }
            }
            if (input.CreateJob && IsPrintingItem(item) && !item.ProductionJobs.Any())
            {
                if (input.EstimatedHours <= 0) ModelState.AddModelError(string.Empty, $"{label}: estimated hours must be greater than zero.");
                else item.ProductionJobs.Add(OrderLineBuilder.NewJob(item.Quantity, input.EstimatedHours, input.Priority));
            }
        }

        var built = await OrderLineBuilder.BuildAsync(_db, Items, CanOverridePrice, CanSetFilePrice, employeeId, ModelState);

        var remaining = order.Items.Where(i => !removed.Contains(i)).ToList();
        if (!remaining.Any() && !built.Items.Any())
            ModelState.AddModelError(string.Empty, "An order must keep at least one item.");

        var newTotal = remaining.Sum(i => i.Quantity * i.UnitPrice) + built.Items.Sum(i => i.Quantity * i.UnitPrice);
        if (newTotal < order.TotalPaid)
            ModelState.AddModelError(string.Empty, $"The new total ({newTotal:N2}) cannot be lower than the payments already received ({order.TotalPaid:N2}).");

        if (!ModelState.IsValid) return await FailAsync(id);

        order.CustomerId = CustomerId;
        foreach (var item in built.Items) order.Items.Add(item);
        foreach (var r in removed)
        {
            _db.PrintFiles.RemoveRange(r.Files);
            _db.ProductionJobs.RemoveRange(r.ProductionJobs);
            _db.OrderItems.Remove(r);
        }
        var removedFilePaths = removed.SelectMany(r => r.Files).Select(f => f.FilePath).ToList();

        var written = new List<string>();
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            if (employeeId.HasValue)
                await _db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'CurrentEmployeeID', @value={employeeId.Value}");
            await _db.SaveChangesAsync();   // updates, deletes, and new items (gets their OrderItemIDs)
            if (built.Files.Any())
            {
                await OrderLineBuilder.AddFilesAsync(_db, _environment.ContentRootPath, built.Files, employeeId ?? 0, written);
                await _db.SaveChangesAsync();
            }
            await tx.CommitAsync();
        }
        catch (Exception ex) when (ex is DbUpdateException or SqlException or IOException or UnauthorizedAccessException)
        {
            await tx.RollbackAsync();
            OrderLineBuilder.CleanupWritten(written);
            ModelState.AddModelError(string.Empty, "The order could not be updated. Nothing was changed. Check file storage permissions and try again.");
            return await FailAsync(id);
        }

        // Physical uploads of removed items are deleted only after the database commit succeeded.
        foreach (var path in removedFilePaths) OrderLineBuilder.DeleteManagedFile(_environment.ContentRootPath, path);

        TempData["Success"] = $"Order #{id} updated. New total {newTotal:N2}" +
                              (removed.Count > 0 ? $", {removed.Count} item(s) removed" : "") +
                              (built.Items.Count > 0 ? $", {built.Items.Count} item(s) added." : ".");
        return RedirectToPage("Details", new { id });
    }

    private async Task<Order?> LoadOrderAsync(int id)
    {
        var order = await _db.Orders
            .Include(o => o.Payments)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.Items).ThenInclude(i => i.Service)
            .Include(o => o.Items).ThenInclude(i => i.Files)
            .Include(o => o.Items).ThenInclude(i => i.ProductionJobs).ThenInclude(j => j.PrinterAssignments)
            .Include(o => o.Items).ThenInclude(i => i.ProductionJobs).ThenInclude(j => j.JobAssignments)
            .Include(o => o.Items).ThenInclude(i => i.ProductionJobs).ThenInclude(j => j.MaterialUsages)
            .Include(o => o.Items).ThenInclude(i => i.ProductionJobs).ThenInclude(j => j.CostBreakdown)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.OrderId == id);
        order?.Items.Sort((a, b) => a.OrderItemId.CompareTo(b.OrderItemId));
        return order;
    }

    private async Task LoadListsAsync()
    {
        CustomersList = await _db.Customers.OrderBy(c => c.Name).ToListAsync();

        // Only products whose linked material currently has stock.
        var inStockMaterialIds = _db.MaterialStockOnHand
            .Where(s => s.CurrentStockLevel > 0)
            .Select(s => s.MaterialId);

        ProductsList = await _db.Products
            .Where(p => p.MaterialId.HasValue &&
                        inStockMaterialIds.Contains(p.MaterialId.Value))
            .OrderBy(p => p.Name)
            .ToListAsync();

        ServicesList = await _db.Services.OrderBy(s => s.Name).ToListAsync();
    }

    // Re-show the form with the database state; typed values for editable rows are kept, selected files are lost.
    private async Task<IActionResult> FailAsync(int id)
    {
        var posted = Existing.ToList();
        var hadFiles = Items.Any(i => i.Files.Any(f => f.Upload != null));
        foreach (var line in Items) line.Files = new();
        if (hadFiles) ModelState.AddModelError(string.Empty, "Please re-select your print files (the browser cannot keep them after an error).");

        _db.ChangeTracker.Clear();   // discard the in-memory edits made before validation failed
        var fresh = await LoadOrderAsync(id);
        if (fresh == null) return NotFound();
        Order = fresh;
        Existing = fresh.Items.Select(i => posted.FirstOrDefault(p => p.OrderItemId == i.OrderItemId)
                       ?? new ExistingItemInput { OrderItemId = i.OrderItemId, Quantity = i.Quantity, UnitPrice = i.UnitPrice }).ToList();
        await LoadListsAsync();
        return Page();
    }

    private IActionResult ErrorRedirect(int id, string message)
    {
        TempData["Error"] = message;
        return RedirectToPage("Details", new { id });
    }

    private async Task<int?> ResolveCurrentEmployeeIdAsync()
    {
        var username = User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(username)) return null;
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username && u.IsActive);
        return user?.EmployeeId;
    }
}
