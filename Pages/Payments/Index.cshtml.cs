using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Payments;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public string? StatusFilter { get; set; }
    public List<Payment> PaymentsList { get; set; } = new();

    public async Task OnGetAsync(string? status)
    {
        StatusFilter = string.IsNullOrWhiteSpace(status) ? null : status;

        var query = _db.Payments.Include(p => p.Order).ThenInclude(o => o!.Customer).AsQueryable();
        if (StatusFilter != null)
            query = query.Where(p => p.PaymentStatus == StatusFilter);

        PaymentsList = await query.OrderByDescending(p => p.PaymentDate).ToListAsync();
    }

    // Matches DomainConstants.PaymentStatuses (PENDING/COMPLETED/FAILED/REFUNDED).
    public string BadgeClass(string status) => status switch
    {
        "PENDING" => "text-bg-warning",
        "COMPLETED" => "text-bg-success",
        "FAILED" => "text-bg-danger",
        "REFUNDED" => "text-bg-secondary",
        _ => "text-bg-light"
    };
}
