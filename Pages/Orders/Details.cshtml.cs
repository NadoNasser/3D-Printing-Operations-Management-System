using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Common;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Orders;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;

    public DetailsModel(AppDbContext db) => _db = db;

    public Order Order { get; set; } = new();

    public IEnumerable<string> AllowedStatuses =>
        DomainConstants.OrderStatuses
            .Where(s => WorkflowRules.IsValidOrderTransition(Order.Status, s));

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var order = await LoadOrderAsync(id);

        if (order == null)
            return NotFound();

        Order = order;
        return Page();
    }

    public async Task<IActionResult> OnPostStatusAsync(int id, string status)
    {
        status = (status ?? "").Trim().ToUpperInvariant();

        var order = await _db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Include(o => o.Items)
                .ThenInclude(i => i.Service)
            .Include(o => o.Items)
                .ThenInclude(i => i.ProductionJobs)
            .Include(o => o.Deliveries)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order == null)
            return NotFound();

        if (!DomainConstants.OrderStatuses.Contains(status))
            return ErrorRedirect(id, "That order status is not valid.");

        if (!WorkflowRules.IsValidOrderTransition(order.Status, status))
            return ErrorRedirect(
                id,
                $"An order cannot move directly from {order.Status} to {status}.");

        if (status == "COMPLETED")
        {
            if (!order.Deliveries.Any(d => d.Status == "DELIVERED"))
                return ErrorRedirect(id, "The order cannot be completed until it has been delivered.");

            if (order.TotalAmount <= 0 || order.Balance > 0.005m)
                return ErrorRedirect(id, "The order cannot be completed until it is fully paid.");
        }

        try
        {
            
        if (status == "CONFIRMED")
            {
                var inventoryError = await ConfirmOrderAndDeductInventoryAsync(order);

                if (inventoryError != null)
                    return ErrorRedirect(id, inventoryError);

                TempData["Success"] =
                    $"Order #{order.OrderId} confirmed successfully. " +
                    "Product inventory was deducted";

                return RedirectToPage("Details", new { id });
            }
        if (status == "CANCELED")
            {
                await CancelOrderAndRestoreInventoryAsync(order);

                TempData["Success"] =
                   $"Order #{order.OrderId} canceled. Product stock was returned and " +
                   "completed payments were marked REFUNDED. Return the money to the customer.";

                return RedirectToPage("Details", new { id });
            }
            order.Status = status;

            await SaveWithEmployeeContextAsync();

            TempData["Success"] =
                $"Order status updated to {status}.";

        }
        catch (DbUpdateException)
        {
            TempData["Error"] =
                "The status could not be saved because the database rejected the change.";
        }
        catch (SqlException ex)
        {
            TempData["Error"] =
                ex.Message.Contains("trigger", StringComparison.OrdinalIgnoreCase)
                    ? "The status could not be saved because the database trigger configuration is incomplete."
                    : "The status could not be saved.";
        }
        catch (Exception)
        {
            TempData["Error"] =
                "The status could not be saved because an unexpected error occurred.";
        }

        return RedirectToPage("Details", new { id });
    }

    private async Task<string?> ConfirmOrderAndDeductInventoryAsync(Order order)
    {
        /*
         * If this order already has SALE transactions, confirmation has
         * already consumed its product inventory.
         *
         * This protects against accidental double deduction, especially
         * when an order returns from ON_HOLD to CONFIRMED.
         */
        var existingSaleOrderItemIds = await _db.InventoryTransactions
            .Where(t =>
                t.OrderItemId.HasValue &&
                t.TransactionType == "SALE" &&
                t.OrderItem!.OrderId == order.OrderId)
            .Select(t => t.OrderItemId!.Value)
            .ToListAsync();

        var productItems = order.Items
            .Where(i => i.ProductId.HasValue)
            .ToList();

        /*
         * Validate that every product is linked to a material.
         */
        var productWithoutMaterial = productItems
            .FirstOrDefault(i => !i.Product!.MaterialId.HasValue);

        if (productWithoutMaterial != null)
        {
            return
                $"Product '{productWithoutMaterial.Product?.Name ?? "Unknown"}' " +
                "is not linked to a material and cannot be confirmed.";
        }

        /*
         * Only product items that do not already have a SALE transaction
         * require a new inventory deduction.
         */
        var itemsToDeduct = productItems
            .Where(i => !existingSaleOrderItemIds.Contains(i.OrderItemId))
            .ToList();

        /*
         * Services are deliberately excluded.
         *
         * Their production jobs can continue through the production
         * workflow, but they do NOT consume inventory here.
         */
        if (!itemsToDeduct.Any())
        {
            order.Status = "CONFIRMED";

            await SaveWithEmployeeContextAsync();
            return null;
        }

        /*
         * Group by MaterialID because multiple products can use the
         * same material.
         *
         * Example:
         *
         * Product A -> Material 10 -> Qty 3
         * Product B -> Material 10 -> Qty 4
         *
         * Required Material 10 = 7, not two independent checks.
         */
        var requiredByMaterial = itemsToDeduct
            .GroupBy(i => i.Product!.MaterialId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(i => (decimal)i.Quantity));

        var materialIds = requiredByMaterial.Keys.ToList();

        /*
         * Use the existing stock-on-hand view.
         *
         * We intentionally do NOT use MATERIALS.CurrentStock because
         * stock is calculated from INVENTORY_TRANSACTIONS.
         */
        var stockRows = await _db.MaterialStockOnHand
            .AsNoTracking()
            .Where(s => materialIds.Contains(s.MaterialId))
            .ToListAsync();

        var stockByMaterial = stockRows
            .ToDictionary(s => s.MaterialId);

        /*
         * Validate ALL materials before inserting ANY SALE transaction.
         */
        foreach (var requirement in requiredByMaterial)
        {
            if (!stockByMaterial.TryGetValue(
                    requirement.Key,
                    out var stock))
            {
                return
                    $"No stock information was found for material ID {requirement.Key}. " +
                    "The order cannot be confirmed.";
            }

            if (stock.CurrentStockLevel < requirement.Value)
            {
                return
                    $"Insufficient stock for material '{stock.MaterialName}' " +
                    $"(SKU: {stock.SKU}). " +
                    $"Required: {requirement.Value:N3} {stock.Unit}, " +
                    $"Available: {stock.CurrentStockLevel:N3} {stock.Unit}.";
            }
        }

        /*
         * Use SERIALIZABLE isolation so two simultaneous confirmations
         * cannot both pass the stock check and consume the same stock.
         */
        await _db.Database.OpenConnectionAsync();

        await using var transaction =
            await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            /*
             * Set employee context on the SAME database connection /
             * transaction used by this confirmation.
             */
            var username = User.FindFirstValue(ClaimTypes.Name);

            var employeeId = username == null
                ? null
                : await _db.Users
                    .AsNoTracking()
                    .Where(u => u.Username == username && u.IsActive)
                    .Select(u => (int?)u.EmployeeId)
                    .FirstOrDefaultAsync();

            if (employeeId.HasValue)
            {
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"EXEC sys.sp_set_session_context @key=N'CurrentEmployeeID', @value={employeeId.Value}");
            }

            /*
             * Re-read stock inside the SERIALIZABLE transaction.
             *
             * This is the authoritative check immediately before
             * creating the SALE transactions.
             */
            var lockedStockRows = await _db.MaterialStockOnHand
                .AsNoTracking()
                .Where(s => materialIds.Contains(s.MaterialId))
                .ToListAsync();

            var lockedStockByMaterial = lockedStockRows
                .ToDictionary(s => s.MaterialId);

            foreach (var requirement in requiredByMaterial)
            {
                if (!lockedStockByMaterial.TryGetValue(
                        requirement.Key,
                        out var stock))
                {
                    await transaction.RollbackAsync();

                    return
                        $"No stock information was found for material ID {requirement.Key}. " +
                        "The order cannot be confirmed.";
                }

                if (stock.CurrentStockLevel < requirement.Value)
                {
                    await transaction.RollbackAsync();

                    return
                        $"Insufficient stock for material '{stock.MaterialName}' " +
                        $"(SKU: {stock.SKU}). " +
                        $"Required: {requirement.Value:N3} {stock.Unit}, " +
                        $"Available: {stock.CurrentStockLevel:N3} {stock.Unit}.";
                }
            }

            /*
             * Create one SALE transaction per PRODUCT order line.
             *
             * Quantity is positive here. The stock view must interpret
             * SALE as an outbound transaction.
             */
            foreach (var item in itemsToDeduct)
            {
                var product = item.Product!;

                var material = await _db.Materials
                    .FirstAsync(m => m.MaterialId == product.MaterialId!.Value);

                _db.InventoryTransactions.Add(
                    new InventoryTransaction
                    {
                        MaterialId = material.MaterialId,
                        TransactionType = "SALE",
                        Quantity = -item.Quantity,
                        UnitCost = material.PurchasePrice,
                        TransactionDate = DateTime.Now,
                        OrderItemId = item.OrderItemId,
                        Reason = "PRODUCT_SALE"
                    });
            }

            order.Status = "CONFIRMED";

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return null;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }

    private async Task CancelOrderAndRestoreInventoryAsync(Order order)
    {
        var sales = await _db.InventoryTransactions
            .Where(t => t.TransactionType == "SALE" &&
                        t.OrderItemId.HasValue &&
                        t.OrderItem!.OrderId == order.OrderId)
            .ToListAsync();

        // Protects against returning the same stock twice.
        var alreadyReturned = await _db.InventoryTransactions
            .Where(t => t.TransactionType == "RETURN" &&
                        t.OrderItemId.HasValue &&
                        t.OrderItem!.OrderId == order.OrderId)
            .Select(t => t.OrderItemId!.Value)
            .ToListAsync();

        foreach (var sale in sales.Where(s => !alreadyReturned.Contains(s.OrderItemId!.Value)))
        {
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                MaterialId = sale.MaterialId,
                TransactionType = "RETURN",
                Quantity = Math.Abs(sale.Quantity),
                UnitCost = sale.UnitCost,
                TransactionDate = DateTime.Now,
                OrderItemId = sale.OrderItemId,
                Reason = "ORDER_CANCELED"
            });
        }

        // Payments received for this order are marked REFUNDED.
        foreach (var payment in order.Payments.Where(p => p.PaymentStatus == "COMPLETED"))
            payment.PaymentStatus = "REFUNDED";

        order.Status = "CANCELED";
        await SaveWithEmployeeContextAsync();
    }

    private async Task<Order?> LoadOrderAsync(int id) =>
        await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Include(o => o.Items)
                .ThenInclude(i => i.Service)
            .Include(o => o.Items)
                .ThenInclude(i => i.ProductionJobs)
            .Include(o => o.Items)
                .ThenInclude(i => i.Files)
            .Include(o => o.Payments)
            .Include(o => o.Deliveries)
            .Include(o => o.StatusHistory)
                .ThenInclude(h => h.ChangedByEmployee)
            .FirstOrDefaultAsync(o => o.OrderId == id);

    private IActionResult ErrorRedirect(int id, string message)
    {
        TempData["Error"] = message;
        return RedirectToPage("Details", new { id });
    }

    private async Task SaveWithEmployeeContextAsync()
    {
        var username = User.FindFirstValue(ClaimTypes.Name);

        var employeeId = username == null
            ? null
            : (await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u => u.Username == username && u.IsActive))
                ?.EmployeeId;

        await _db.Database.OpenConnectionAsync();

        try
        {
            if (employeeId.HasValue)
            {
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"EXEC sys.sp_set_session_context @key=N'CurrentEmployeeID', @value={employeeId.Value}");
            }

            await _db.SaveChangesAsync();
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }
}
