using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Files;

public class EditModel : PageModel
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".stl", ".3mf", ".obj", ".step", ".stp" };

    private const long MaxFileSize = 100L * 1024 * 1024;

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public EditModel(AppDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    [BindProperty(SupportsGet = true)]
    public int FileId { get; set; }

    [BindProperty]
    public int OrderItemId { get; set; }

    [BindProperty]
    public string FileName { get; set; } = "";

    [BindProperty]
    public string? FileExtension { get; set; }

    [BindProperty]
    public string? ExternalFilePath { get; set; }

    [BindProperty]
    public string? Notes { get; set; }

    [BindProperty]
    public bool IsCurrent { get; set; }

    [BindProperty]
    public IFormFile? ReplacementFile { get; set; }

    public string ItemLabel { get; set; } = "";
    public int OrderId { get; set; }
    public string ExistingLocation { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(int fileId)
    {
        var file = await _db.PrintFiles
            .Include(f => f.OrderItem)
                .ThenInclude(i => i!.Product)
            .Include(f => f.OrderItem)
                .ThenInclude(i => i!.Service)
            .FirstOrDefaultAsync(f => f.FileId == fileId);

        if (file == null || file.OrderItem == null)
            return NotFound();

        FileId = file.FileId;
        OrderItemId = file.OrderItemId;
        OrderId = file.OrderItem.OrderId;
        ItemLabel = file.OrderItem.Product?.Name ?? file.OrderItem.Service?.Name ?? "";
        FileName = file.FileName;
        FileExtension = file.FileExtension;
        Notes = file.Notes;
        IsCurrent = file.IsCurrent;
        ExistingLocation = file.FilePath;

        if (file.FilePath.StartsWith("/app-data/print-files/", StringComparison.OrdinalIgnoreCase))
            ExternalFilePath = null;
        else
            ExternalFilePath = file.FilePath;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var file = await _db.PrintFiles
            .Include(f => f.OrderItem)
                .ThenInclude(i => i!.Product)
            .Include(f => f.OrderItem)
                .ThenInclude(i => i!.Service)
            .FirstOrDefaultAsync(f => f.FileId == FileId);

        if (file == null || file.OrderItem == null)
            return NotFound();

        OrderItemId = file.OrderItemId;
        OrderId = file.OrderItem.OrderId;
        ItemLabel = file.OrderItem.Product?.Name ?? file.OrderItem.Service?.Name ?? "";
        ExistingLocation = file.FilePath;

        var uploadedBy = await ResolveCurrentEmployeeIdAsync();
        if (uploadedBy == null)
            ModelState.AddModelError(string.Empty, "Your login is not linked to an Employee record.");

        FileName = Path.GetFileNameWithoutExtension(FileName?.Trim() ?? "");
        if (string.IsNullOrWhiteSpace(FileName))
            ModelState.AddModelError(nameof(FileName), "File name is required.");

        var hasReplacement = ReplacementFile is { Length: > 0 };
        var hasExternalPath = !string.IsNullOrWhiteSpace(ExternalFilePath);

        if (hasReplacement && hasExternalPath)
            ModelState.AddModelError(string.Empty, "Use a replacement upload or an external path, not both.");

        if (hasReplacement)
        {
            var extension = Path.GetExtension(ReplacementFile!.FileName);
            if (!AllowedExtensions.Contains(extension))
                ModelState.AddModelError(nameof(ReplacementFile), "Allowed design extensions: .stl, .3mf, .obj, .step, .stp.");

            if (ReplacementFile.Length > MaxFileSize)
                ModelState.AddModelError(nameof(ReplacementFile), "The replacement file cannot exceed 100 MB.");

            FileExtension = extension.ToLowerInvariant();
        }
        else if (!string.IsNullOrWhiteSpace(FileExtension))
        {
            FileExtension = FileExtension.Trim();
            if (!FileExtension.StartsWith('.'))
                FileExtension = "." + FileExtension;

            if (!AllowedExtensions.Contains(FileExtension))
                ModelState.AddModelError(nameof(FileExtension), "Allowed design extensions: .stl, .3mf, .obj, .step, .stp.");
        }
        else
        {
            FileExtension = file.FileExtension;
        }

        if (!ModelState.IsValid)
            return Page();

        var oldPhysicalPath = GetManagedPhysicalPath(file.FilePath);
        string? newPhysicalPath = null;
        string? newRelativePath = null;

        try
        {
            if (hasReplacement)
            {
                var extension = Path.GetExtension(ReplacementFile!.FileName).ToLowerInvariant();
                var relativeDirectory = Path.Combine("App_Data", "PrintFiles", OrderItemId.ToString());
                var physicalDirectory = Path.Combine(_environment.ContentRootPath, relativeDirectory);
                Directory.CreateDirectory(physicalDirectory);

                var storedName = $"v{file.FileVersion}_{Guid.NewGuid():N}{extension}";
                newPhysicalPath = Path.Combine(physicalDirectory, storedName);
                newRelativePath = "/app-data/print-files/" + OrderItemId + "/" + storedName;

                await using (var input = ReplacementFile.OpenReadStream())
                await using (var output = System.IO.File.Create(newPhysicalPath))
                {
                    await input.CopyToAsync(output);
                }

                string hash;
                await using (var hashStream = System.IO.File.OpenRead(newPhysicalPath))
                {
                    hash = Convert.ToHexString(await SHA256.HashDataAsync(hashStream));
                }

                file.FilePath = newRelativePath;
                file.FileSizeBytes = ReplacementFile.Length;
                file.FileHash = hash;
                file.UploadedAt = DateTime.Now;
                file.UploadedBy = uploadedBy!.Value;
                file.FileExtension = extension;
            }
            else if (hasExternalPath)
            {
                file.FilePath = ExternalFilePath!.Trim();
                file.FileSizeBytes = null;
                file.FileHash = null;
            }

            file.FileName = FileName;
            file.FileExtension = FileExtension;
            file.Notes = Notes;

            if (IsCurrent)
            {
                var otherFiles = await _db.PrintFiles
                    .Where(f => f.OrderItemId == OrderItemId && f.FileId != file.FileId && f.IsCurrent)
                    .ToListAsync();
                foreach (var other in otherFiles)
                    other.IsCurrent = false;
            }
            else
            {
                file.IsCurrent = false;
            }

            await _db.SaveChangesAsync();

            if (newPhysicalPath != null && oldPhysicalPath != null &&
                !string.Equals(newPhysicalPath, oldPhysicalPath, StringComparison.OrdinalIgnoreCase) &&
                System.IO.File.Exists(oldPhysicalPath))
            {
                System.IO.File.Delete(oldPhysicalPath);
            }

            TempData["Success"] = "Print file updated successfully.";
            return RedirectToPage("Index", new { orderItemId = OrderItemId });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DbUpdateException)
        {
            if (newPhysicalPath != null && System.IO.File.Exists(newPhysicalPath))
                System.IO.File.Delete(newPhysicalPath);

            ModelState.AddModelError(string.Empty, "The print file could not be updated. Please check storage permissions and database constraints.");
            return Page();
        }
    }

    private string? GetManagedPhysicalPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) ||
            !filePath.StartsWith("/app-data/print-files/", StringComparison.OrdinalIgnoreCase))
            return null;

        var relativePath = filePath["/app-data/print-files/".Length..]
            .Replace('/', Path.DirectorySeparatorChar);
        var storageRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "App_Data", "PrintFiles"));
        var physical = Path.GetFullPath(Path.Combine(storageRoot, relativePath));
        var storagePrefix = storageRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        return physical.StartsWith(storagePrefix, StringComparison.OrdinalIgnoreCase) ? physical : null;
    }

    private async Task<int?> ResolveCurrentEmployeeIdAsync()
    {
        var username = User.Identity?.Name;
        if (username == null) return null;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username && u.IsActive);
        return user?.EmployeeId;
    }
}
