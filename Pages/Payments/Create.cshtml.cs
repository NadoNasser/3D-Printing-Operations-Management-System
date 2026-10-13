using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Common;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Payments;

public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    public CreateModel(AppDbContext db) => _db = db;
    [BindProperty] public int OrderId { get; set; }
    [BindProperty] public Payment Payment { get; set; } = new() { PaymentMethod = "CASH", PaymentStatus = "COMPLETED" };
    public decimal RemainingBalance { get; set; }

    public async Task<IActionResult> OnGetAsync(int orderId)
    {
        var order = await LoadOrderAsync(orderId);
        if (order == null) return NotFound();
        OrderId = orderId;
        RemainingBalance = order.Balance;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var order = await LoadOrderAsync(OrderId);
        if (order == null) return NotFound();
        RemainingBalance = order.Balance;
        if (Payment.Amount <= 0) ModelState.AddModelError(string.Empty, "Amount must be greater than zero.");
        if (!DomainConstants.PaymentStatuses.Contains(Payment.PaymentStatus)) ModelState.AddModelError(string.Empty, "Invalid payment status.");
        if (!DomainConstants.PaymentMethods.Contains(Payment.PaymentMethod)) ModelState.AddModelError(string.Empty, "Invalid payment method.");
        if (Payment.PaymentStatus == "COMPLETED" && Payment.Amount > RemainingBalance + 0.005m)
            ModelState.AddModelError(string.Empty, $"The completed payment cannot exceed the remaining balance of {RemainingBalance:N2}.");
        if (!ModelState.IsValid) return Page();

        Payment.OrderId = OrderId;
        Payment.PaymentDate = DateTime.Now;
        _db.Payments.Add(Payment);
        try { await _db.SaveChangesAsync(); }
        catch (Microsoft.Data.SqlClient.SqlException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message.Contains("exceed", StringComparison.OrdinalIgnoreCase) ? "The payment exceeds the remaining order balance." : "The payment could not be saved.");
            return Page();
        }
        var completed = await OrderAutoStatus.TryCompleteAsync(_db, OrderId, User);
        TempData["Success"] = completed ? "Payment recorded. The order is delivered and fully paid, so it is now COMPLETED." : "Payment recorded.";
        return RedirectToPage("/Orders/Details", new { id = OrderId });
    }

    private async Task<Order?> LoadOrderAsync(int id) => await _db.Orders.Include(o => o.Items).Include(o => o.Payments).FirstOrDefaultAsync(o => o.OrderId == id);
}
