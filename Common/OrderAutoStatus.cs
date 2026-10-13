using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Data;

namespace PrintShop3D.Common;

public static class OrderAutoStatus
{
    
    public static async Task<bool> TryCompleteAsync(AppDbContext db, int orderId, ClaimsPrincipal user)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.Deliveries)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        if (order == null || order.Status != "CONFIRMED") return false;
        if (!order.Deliveries.Any(d => d.Status == "DELIVERED")) return false;
        if (order.TotalAmount <= 0 || order.Balance > 0.005m) return false;
        if (!WorkflowRules.IsValidOrderTransition(order.Status, "COMPLETED")) return false;

        order.Status = "COMPLETED";

        // Same pattern as Orders/Details: tell the status-history trigger who made the change.
        var username = user.FindFirstValue(ClaimTypes.Name);
        var employeeId = username == null ? null
            : (await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username && u.IsActive))?.EmployeeId;

        await db.Database.OpenConnectionAsync();
        try
        {
            if (employeeId.HasValue)
                await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'CurrentEmployeeID', @value={employeeId.Value}");
            await db.SaveChangesAsync();
        }
        finally { await db.Database.CloseConnectionAsync(); }
        return true;
    }
}
