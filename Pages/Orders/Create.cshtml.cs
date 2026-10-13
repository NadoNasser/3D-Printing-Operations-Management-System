using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Common;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Orders;

public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;
    public CreateModel(AppDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    [BindProperty] public int CustomerId { get; set; }
    [BindProperty] public List<OrderLineInput> Items { get; set; } = new() { new OrderLineInput() };
    public List<Customer> CustomersList { get; set; } = new();
    public List<Product> ProductsList { get; set; } = new();
    public List<Service> ServicesList { get; set; } = new();
    public Customer? SelectedCustomer { get; set; }

    // Unchanged rule: only Admin can override the price of a normal product/service line.
    public bool CanOverridePrice => User.IsInRole("Admin");
    // The price of each print file is set per file by whoever creates the order.
    // Use `User.IsInRole("Admin")` if only admins should be allowed to price files.
    public bool CanSetFilePrice => true;

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

    public async Task OnGetAsync() => await LoadListsAsync();

    // Kept for compatibility; the page adds lines in the browser so selected files are not lost.
    public async Task<IActionResult> OnPostAddLineAsync()
    {
        Items.Add(new OrderLineInput());
        await LoadListsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (CustomerId == 0) ModelState.AddModelError(string.Empty, "Please choose a customer.");
        else
        {
            SelectedCustomer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == CustomerId);
            if (SelectedCustomer == null) ModelState.AddModelError(string.Empty, "The selected customer no longer exists.");
        }
        if (!ModelState.IsValid) return await FailAsync();

        var uploadedBy = await ResolveCurrentEmployeeIdAsync();
        var built = await OrderLineBuilder.BuildAsync(_db, Items, CanOverridePrice, CanSetFilePrice, uploadedBy, ModelState);
        if (!built.Items.Any()) ModelState.AddModelError(string.Empty, "Add at least one order line with a quantity greater than zero.");
        if (!ModelState.IsValid) return await FailAsync();

        var order = new Order { CustomerId = CustomerId, Status = "DRAFT", CreatedAt = DateTime.Now };
        foreach (var item in built.Items) order.Items.Add(item);

        var written = new List<string>();
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();   // generates the OrderItemIDs used as file folder names
            await OrderLineBuilder.AddFilesAsync(_db, _environment.ContentRootPath, built.Files, uploadedBy ?? 0, written);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (Exception ex) when (ex is DbUpdateException or SqlException or IOException or UnauthorizedAccessException)
        {
            await tx.RollbackAsync();
            OrderLineBuilder.CleanupWritten(written);
            ModelState.AddModelError(string.Empty, "The order could not be saved. Check that each line is a valid product or service, and that file storage is writable.");
            return await FailAsync();
        }

        var jobCount = order.Items.Sum(i => i.ProductionJobs.Count);
        TempData["Success"] = $"Order created as DRAFT. Total {order.Items.Sum(i => i.Quantity * i.UnitPrice):N2}" +
                              (built.Files.Count > 0 ? $", {built.Files.Count} print file(s)" : "") +
                              (jobCount > 0 ? $", {jobCount} production job(s) queued." : ".");
        return RedirectToPage("Details", new { id = order.OrderId });
    }

    // Browsers cannot re-populate file inputs, so selected files are dropped when the page is shown again.
    private async Task<IActionResult> FailAsync()
    {
        var hadFiles = Items.Any(i => i.Files.Any(f => f.Upload != null));
        foreach (var line in Items) line.Files = new();
        if (hadFiles) ModelState.AddModelError(string.Empty, "Please re-select your print files (the browser cannot keep them after an error).");
        await LoadListsAsync();
        return Page();
    }

    private async Task<int?> ResolveCurrentEmployeeIdAsync()
    {
        var username = User.Identity?.Name;
        if (username == null) return null;
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username && u.IsActive);
        return user?.EmployeeId;
    }
}
