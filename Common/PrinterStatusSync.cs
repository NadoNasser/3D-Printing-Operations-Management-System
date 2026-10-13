using Microsoft.EntityFrameworkCore;
using PrintShop3D.Data;

namespace PrintShop3D.Common;

/// <summary>
/// ONE place that decides a printer's status. Used by the Printers list and by both Production Job pages,
/// which previously each had their own (slightly different) copy of this logic.
/// Rules:
///  - OUT_OF_SERVICE / INACTIVE are manual and never touched.
///  - A maintenance record that is IN_PROGRESS forces MAINTENANCE.
///  - A printer that is already MAINTENANCE stays MAINTENANCE (e.g. set manually) until someone changes it
///    or completes its maintenance on the Maintenance page. (The old Printers-list code reset it to AVAILABLE.)
///  - BUSY when an assignment is PRINTING (its job is in progress, even after the planned end time passed)
///    or a SCHEDULED assignment whose time window covers "now".
///  - Otherwise AVAILABLE.
/// </summary>
public static class PrinterStatusSync
{
    public static async Task SynchronizeAsync(AppDbContext db)
    {
        var now = DateTime.Now;
        var printers = await db.Printers.ToListAsync();
        var assignments = await db.PrinterAssignments
            .Where(a => a.Status == "SCHEDULED" || a.Status == "PRINTING")
            .ToListAsync();
        var maintenanceAssets = (await db.AssetMaintenances
            .Where(m => m.Status == "IN_PROGRESS")
            .Select(m => m.AssetId)
            .ToListAsync()).ToHashSet();

        var changed = false;
        foreach (var printer in printers)
        {
            if (printer.Status is "OUT_OF_SERVICE" or "INACTIVE") continue;

            string desired;
            if (maintenanceAssets.Contains(printer.AssetId) || printer.Status == "MAINTENANCE")
                desired = "MAINTENANCE";
            else
            {
                var busy = assignments.Any(a => a.PrinterId == printer.PrinterId &&
                    (a.Status == "PRINTING" || (a.AssignedStart <= now && a.AssignedEnd > now)));
                desired = busy ? "BUSY" : "AVAILABLE";
            }

            if (printer.Status != desired) { printer.Status = desired; changed = true; }
        }
        if (changed) await db.SaveChangesAsync();
    }
}
