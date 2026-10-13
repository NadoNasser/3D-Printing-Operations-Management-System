using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Files;

public class CreateModel : PageModel
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".stl", ".3mf", ".obj", ".step", ".stp" };

    private const long MaxFileSize = 100L * 1024 * 1024;
    private const long MaxTotalUploadSize = 300L * 1024 * 1024;

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public CreateModel(AppDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    [BindProperty]
    public int OrderItemId { get; set; }

    // Multiple files are stored as separate PRINT_FILES rows. FileVersion remains
    // the upload sequence for this order item, and the newest uploaded file is
    // marked as the current version to preserve the existing schema semantics.
    [BindProperty]
    public List<IFormFile> Uploads { get; set; } = new();

    [BindProperty]
    public string? ExternalFilePath { get; set; }

    [BindProperty]
    public string? FileName { get; set; }

    [BindProperty]
    public string? FileExtension { get; set; }

    [BindProperty]
    public string? Notes { get; set; }

    public string ItemLabel { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(int orderItemId)
    {
        var item = await _db.OrderItems
            .Include(i => i.Product)
            .Include(i => i.Service)
            .FirstOrDefaultAsync(i => i.OrderItemId == orderItemId);

        if (item == null) return NotFound();

        OrderItemId = orderItemId;
        ItemLabel = item.Product?.Name ?? item.Service?.Name ?? "";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var uploadedBy = await ResolveCurrentEmployeeIdAsync();
        var item = await _db.OrderItems
            .Include(i => i.Product)
            .Include(i => i.Service)
            .FirstOrDefaultAsync(i => i.OrderItemId == OrderItemId);

        if (item == null) return NotFound();

        ItemLabel = item.Product?.Name ?? item.Service?.Name ?? "";

        if (uploadedBy == null)
            ModelState.AddModelError(string.Empty, "Your login is not linked to an Employee record.");

        var validUploads = Uploads
            .Where(f => f != null && f.Length > 0)
            .ToList();

        var hasExternalPath = !string.IsNullOrWhiteSpace(ExternalFilePath);
        if (!validUploads.Any() && !hasExternalPath)
            ModelState.AddModelError(string.Empty, "Select at least one design file or provide an external file path.");

        if (validUploads.Any() && hasExternalPath)
            ModelState.AddModelError(string.Empty, "Use uploaded files or an external file path, not both at the same time.");

        if (validUploads.Any())
        {
            var totalSize = validUploads.Sum(f => f.Length);
            if (totalSize > MaxTotalUploadSize)
                ModelState.AddModelError(string.Empty, "The total upload size cannot exceed 300 MB.");

            foreach (var upload in validUploads)
            {
                var extension = Path.GetExtension(upload.FileName);
                if (!AllowedExtensions.Contains(extension))
                    ModelState.AddModelError(string.Empty, $"{Path.GetFileName(upload.FileName)} is not a supported design file. Allowed: .stl, .3mf, .obj, .step, .stp.");

                if (upload.Length > MaxFileSize)
                    ModelState.AddModelError(string.Empty, $"{Path.GetFileName(upload.FileName)} exceeds the 100 MB per-file limit.");
            }
        }

        if (!ModelState.IsValid)
            return Page();

        var existing = await _db.PrintFiles
            .Where(f => f.OrderItemId == OrderItemId)
            .ToListAsync();
        var nextVersion = existing.Any() ? existing.Max(f => f.FileVersion) + 1 : 1;

        foreach (var existingFile in existing.Where(f => f.IsCurrent))
            existingFile.IsCurrent = false;

        var createdPhysicalPaths = new List<string>();

        try
        {
            if (validUploads.Any())
            {
                for (var uploadIndex = 0; uploadIndex < validUploads.Count; uploadIndex++)
                {
                    var upload = validUploads[uploadIndex];
                    var extension = Path.GetExtension(upload.FileName).ToLowerInvariant();
                    var originalName = Path.GetFileNameWithoutExtension(upload.FileName);
                    var displayName = string.IsNullOrWhiteSpace(originalName) ? "design" : originalName.Trim();
                    var relativeDirectory = Path.Combine("App_Data", "PrintFiles", OrderItemId.ToString());
                    var physicalDirectory = Path.Combine(_environment.ContentRootPath, relativeDirectory);
                    Directory.CreateDirectory(physicalDirectory);

                    var storedName = $"v{nextVersion}_{Guid.NewGuid():N}{extension}";
                    var physicalPath = Path.Combine(physicalDirectory, storedName);
                    createdPhysicalPaths.Add(physicalPath);

                    await using (var input = upload.OpenReadStream())
                    await using (var output = System.IO.File.Create(physicalPath))
                    {
                        await input.CopyToAsync(output);
                    }

                    string hash;
                    await using (var hashStream = System.IO.File.OpenRead(physicalPath))
                    {
                        hash = Convert.ToHexString(await SHA256.HashDataAsync(hashStream));
                    }

                    var fileVersion = nextVersion++;
                    _db.PrintFiles.Add(new PrintFile
                    {
                        OrderItemId = OrderItemId,
                        FileName = displayName,
                        FileExtension = extension,
                        FilePath = "/app-data/print-files/" + OrderItemId + "/" + storedName,
                        FileSizeBytes = upload.Length,
                        FileVersion = fileVersion,
                        // Only the last uploaded file is the current version. This
                        // keeps compatibility with the existing one-current-file schema.
                        IsCurrent = uploadIndex == validUploads.Count - 1,
                        UploadedAt = DateTime.Now,
                        UploadedBy = uploadedBy!.Value,
                        FileHash = hash,
                        Notes = Notes
                    });
                }

                await _db.SaveChangesAsync();
                TempData["Success"] = $"{validUploads.Count} design file(s) saved successfully.";
            }
            else
            {
                var externalPath = ExternalFilePath!.Trim();
                var extension = string.IsNullOrWhiteSpace(FileExtension)
                    ? Path.GetExtension(externalPath)
                    : FileExtension.Trim();

                if (!string.IsNullOrWhiteSpace(extension) && !extension.StartsWith('.'))
                    extension = "." + extension;

                if (!string.IsNullOrWhiteSpace(extension) && !AllowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError(string.Empty, "Allowed design extensions: .stl, .3mf, .obj, .step, .stp.");
                    return Page();
                }

                var displayName = string.IsNullOrWhiteSpace(FileName)
                    ? Path.GetFileNameWithoutExtension(externalPath)?.Trim() ?? "design"
                    : Path.GetFileNameWithoutExtension(FileName.Trim());

                _db.PrintFiles.Add(new PrintFile
                {
                    OrderItemId = OrderItemId,
                    FileName = string.IsNullOrWhiteSpace(displayName) ? "design" : displayName,
                    FileExtension = extension,
                    FilePath = externalPath,
                    FileVersion = nextVersion,
                    IsCurrent = true,
                    UploadedAt = DateTime.Now,
                    UploadedBy = uploadedBy!.Value,
                    Notes = Notes
                });

                await _db.SaveChangesAsync();
                TempData["Success"] = $"External design file saved as version {nextVersion}.";
            }

            return RedirectToPage("Index", new { orderItemId = OrderItemId });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DbUpdateException)
        {
            foreach (var physicalPath in createdPhysicalPaths)
            {
                if (System.IO.File.Exists(physicalPath))
                    System.IO.File.Delete(physicalPath);
            }

            ModelState.AddModelError(string.Empty, "The file(s) could not be saved. Please check storage permissions and database constraints.");
            return Page();
        }
    }

    private async Task<int?> ResolveCurrentEmployeeIdAsync()
    {
        var username = User.Identity?.Name;
        if (username == null) return null;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username && u.IsActive);
        return user?.EmployeeId;
    }
}
