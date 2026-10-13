using System.ComponentModel.DataAnnotations;

namespace PrintShop3D.Models;

// Maps to the new PRINT_FILES table (see db/004_create_print_files_table.sql).
// FilePath may point to an application-managed upload under /uploads or an
// external shared-drive/NAS location. Uploaded files are stored with generated
// server-side names while the original display name remains in FileName.
public class PrintFile
{
    public int FileId { get; set; }
    public int OrderItemId { get; set; }
    [Required, StringLength(255)] public string FileName { get; set; } = "";
    [Required, StringLength(500)] public string FilePath { get; set; } = "";
    [StringLength(20)] public string? FileExtension { get; set; }
    public long? FileSizeBytes { get; set; }
    public int FileVersion { get; set; } = 1;
    public bool IsCurrent { get; set; } = true;
    public DateTime UploadedAt { get; set; }
    public int UploadedBy { get; set; }
    [StringLength(100)] public string? FileHash { get; set; }
    public string? Notes { get; set; }

    public OrderItem? OrderItem { get; set; }
    public Employee? UploadedByEmployee { get; set; }
}
