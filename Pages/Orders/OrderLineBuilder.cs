using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Common;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Orders;

/// <summary>Shared by Orders/Create and Orders/Edit so both pages apply exactly the same rules.</summary>
public static class OrderLineBuilder
{
    public static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".stl", ".3mf", ".obj", ".step", ".stp" };
    public const long MaxFileSize = 100L * 1024 * 1024;
    public const long MaxTotalUploadSize = 300L * 1024 * 1024;
    private const string ManagedPrefix = "/app-data/print-files/";

    public sealed class BuildResult
    {
        public List<OrderItem> Items { get; } = new();
        public List<(OrderItem Item, IFormFile Upload)> Files { get; } = new();
        public long TotalUploadSize { get; set; }
    }

    public static async Task<BuildResult> BuildAsync(
        AppDbContext db, IEnumerable<OrderLineInput> lines, bool canOverridePrice, bool canSetFilePrice,
        int? uploadedBy, ModelStateDictionary ms)
    {
        var result = new BuildResult();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.ItemId)) continue;
            if (line.ItemId.Length < 2 || !int.TryParse(line.ItemId[1..], out var id))
            {
                ms.AddModelError(string.Empty, "One of the selected order items is invalid.");
                continue;
            }

            var uploads = line.Files.Where(f => f.Upload is { Length: > 0 }).ToList();
            var isProduct = line.ItemId.StartsWith('p');

            decimal catalogPrice;
            var isPrinting = false;
            if (isProduct)
            {
                var product = await db.Products.FindAsync(id);
                if (product == null) { ms.AddModelError(string.Empty, "A selected product no longer exists."); continue; }
                catalogPrice = product.BasePrice;
            }
            else
            {
                var service = await db.Services.FindAsync(id);
                if (service == null) { ms.AddModelError(string.Empty, "A selected service no longer exists."); continue; }
                catalogPrice = service.BaseRate;
                isPrinting = ServiceRules.IsPrintingService(service);
            }

            // Printing service WITH print files: one order item (+ optional job) per file.
            if (uploads.Any())
            {
                if (!isPrinting) { ms.AddModelError(string.Empty, "Print files can only be attached to a printing service."); continue; }
                if (uploadedBy == null) { ms.AddModelError(string.Empty, "Your login is not linked to an Employee record, so files cannot be uploaded."); continue; }

                foreach (var f in uploads)
                {
                    var upload = f.Upload!;
                    var safeName = Path.GetFileName(upload.FileName);
                    var valid = true;
                    if (!AllowedExtensions.Contains(Path.GetExtension(upload.FileName))) { ms.AddModelError(string.Empty, $"{safeName} is not a supported design file. Allowed: .stl, .3mf, .obj, .step, .stp."); valid = false; }
                    if (upload.Length > MaxFileSize) { ms.AddModelError(string.Empty, $"{safeName} exceeds the 100 MB per-file limit."); valid = false; }
                    if (f.Quantity < 1) { ms.AddModelError(string.Empty, $"{safeName}: quantity must be at least 1."); valid = false; }
                    if (f.UnitPrice < 0) { ms.AddModelError(string.Empty, $"{safeName}: price cannot be negative."); valid = false; }
                    if (f.EstimatedHours <= 0) { ms.AddModelError(string.Empty, $"{safeName}: estimated hours must be greater than zero."); valid = false; }
                    if (!valid) continue;

                    result.TotalUploadSize += upload.Length;
                    var item = new OrderItem { ServiceId = id, Quantity = f.Quantity, UnitPrice = canSetFilePrice ? f.UnitPrice : catalogPrice };
                    if (line.CreateJob) item.ProductionJobs.Add(NewJob(f.Quantity, f.EstimatedHours, line.Priority));
                    result.Items.Add(item);
                    result.Files.Add((item, upload));
                }
                continue;
            }

            // Normal line (product, service, or printing service without files).
            if (line.Quantity <= 0) continue;
            var plain = new OrderItem
            {
                ProductId = isProduct ? id : null,
                ServiceId = isProduct ? null : id,
                Quantity = line.Quantity,
                UnitPrice = canOverridePrice && line.UnitPrice >= 0 ? line.UnitPrice : catalogPrice
            };
            if (isPrinting && line.CreateJob)
            {
                if (line.EstimatedHours <= 0) { ms.AddModelError(string.Empty, "Estimated hours must be greater than zero for a production job."); continue; }
                plain.ProductionJobs.Add(NewJob(line.Quantity, line.EstimatedHours, line.Priority));
            }
            result.Items.Add(plain);
        }

        if (result.TotalUploadSize > MaxTotalUploadSize)
            ms.AddModelError(string.Empty, "The total upload size cannot exceed 300 MB.");
        return result;
    }

    public static ProductionJob NewJob(int quantity, decimal estimatedHours, int priority) => new()
    {
        Quantity = quantity,
        EstimatedTime = estimatedHours,
        Status = "QUEUED",
        Priority = DomainConstants.ProductionJobPriorities.Contains(priority) ? priority : 2,
        LastUpdatedAt = DateTime.Now
    };

    /// <summary>Writes the uploads to App_Data/PrintFiles/{OrderItemId} and adds PRINT_FILES rows (caller saves).</summary>
    public static async Task AddFilesAsync(AppDbContext db, string contentRoot,
        IEnumerable<(OrderItem Item, IFormFile Upload)> pending, int uploadedBy, List<string> writtenPaths)
    {
        foreach (var (item, upload) in pending)
        {
            var ext = Path.GetExtension(upload.FileName).ToLowerInvariant();
            var displayName = Path.GetFileNameWithoutExtension(upload.FileName).Trim();
            if (string.IsNullOrWhiteSpace(displayName)) displayName = "design";
            if (displayName.Length > 255) displayName = displayName[..255];

            var directory = Path.Combine(contentRoot, "App_Data", "PrintFiles", item.OrderItemId.ToString());
            Directory.CreateDirectory(directory);
            var storedName = $"v1_{Guid.NewGuid():N}{ext}";
            var physicalPath = Path.Combine(directory, storedName);
            writtenPaths.Add(physicalPath);

            await using (var input = upload.OpenReadStream())
            await using (var output = File.Create(physicalPath))
                await input.CopyToAsync(output);

            string hash;
            await using (var hs = File.OpenRead(physicalPath))
                hash = Convert.ToHexString(await SHA256.HashDataAsync(hs));

            db.PrintFiles.Add(new PrintFile
            {
                OrderItemId = item.OrderItemId,
                FileName = displayName,
                FileExtension = ext,
                FilePath = ManagedPrefix + item.OrderItemId + "/" + storedName,
                FileSizeBytes = upload.Length,
                FileVersion = 1,
                IsCurrent = true,
                UploadedAt = DateTime.Now,
                UploadedBy = uploadedBy,
                FileHash = hash
            });
        }
    }

    public static void CleanupWritten(IEnumerable<string> paths)
    {
        foreach (var p in paths)
        {
            try
            {
                if (File.Exists(p)) File.Delete(p);
                var dir = Path.GetDirectoryName(p);
                if (dir != null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
            }
            catch (IOException) { /* best effort */ }
            catch (UnauthorizedAccessException) { /* best effort */ }
        }
    }

    /// <summary>Deletes an application-managed upload (never external NAS paths), constrained to App_Data/PrintFiles.</summary>
    public static void DeleteManagedFile(string contentRoot, string filePath)
    {
        if (!filePath.StartsWith(ManagedPrefix, StringComparison.OrdinalIgnoreCase)) return;
        var relative = filePath[ManagedPrefix.Length..].Replace('/', Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(Path.Combine(contentRoot, "App_Data", "PrintFiles"));
        var physical = Path.GetFullPath(Path.Combine(root, relative));
        if (!physical.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return;
        CleanupWritten(new[] { physical });
    }
}
