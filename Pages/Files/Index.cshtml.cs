using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Files;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public IndexModel(AppDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    public int OrderItemId { get; set; }
    public int OrderId { get; set; }
    public string ItemLabel { get; set; } = "";
    public List<PrintFile> FilesList { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int orderItemId)
    {
        var item = await _db.OrderItems
            .Include(i => i.Product)
            .Include(i => i.Service)
            .FirstOrDefaultAsync(i => i.OrderItemId == orderItemId);

        if (item == null) return NotFound();

        OrderItemId = orderItemId;
        OrderId = item.OrderId;
        ItemLabel = item.Product?.Name ?? item.Service?.Name ?? "";
        FilesList = await _db.PrintFiles
            .Include(f => f.UploadedByEmployee)
            .Where(f => f.OrderItemId == orderItemId)
            .OrderByDescending(f => f.FileVersion)
            .ThenByDescending(f => f.UploadedAt)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnGetDownloadAsync(int fileId, int orderItemId)
    {
        var file = await _db.PrintFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.FileId == fileId && f.OrderItemId == orderItemId);

        // Prevent an authenticated user from downloading a file by changing only
        // the fileId in the URL. The file must belong to the requested order item.
        if (file == null || string.IsNullOrWhiteSpace(file.FilePath)) return NotFound();
        if (!file.FilePath.StartsWith("/app-data/print-files/", StringComparison.OrdinalIgnoreCase)) return NotFound();

        var relativePath = file.FilePath["/app-data/print-files/".Length..]
            .Replace('/', Path.DirectorySeparatorChar);
        var storageRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "App_Data", "PrintFiles"));
        var physical = Path.GetFullPath(Path.Combine(storageRoot, relativePath));
        var storagePrefix = storageRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!physical.StartsWith(storagePrefix, StringComparison.OrdinalIgnoreCase)) return NotFound();
        if (!System.IO.File.Exists(physical)) return NotFound();

        var downloadName = Path.GetFileName(file.FileName) + (file.FileExtension ?? "");
        return PhysicalFile(physical, "application/octet-stream", downloadName);
    }
}
