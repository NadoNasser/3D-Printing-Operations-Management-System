using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Data;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Customers;

public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    public EditModel(AppDbContext db) => _db = db;
    [BindProperty] public Customer Customer { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var c = await _db.Customers.Include(x => x.Addresses).FirstOrDefaultAsync(x => x.CustomerId == id);
        if (c == null) return NotFound(); Customer = c; return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var existing = await _db.Customers.FirstOrDefaultAsync(x => x.CustomerId == Customer.CustomerId);
        if (existing == null) return NotFound();
        if (string.IsNullOrWhiteSpace(Customer.Name) || string.IsNullOrWhiteSpace(Customer.CustomerCode))
        {
            ModelState.AddModelError(string.Empty, "Customer code and name are required.");
            Customer = await _db.Customers.Include(x => x.Addresses).FirstAsync(x => x.CustomerId == Customer.CustomerId);
            return Page();
        }
        existing.Name = Customer.Name.Trim(); existing.Phone = Customer.Phone?.Trim(); existing.Email = Customer.Email?.Trim(); existing.Company = Customer.Company?.Trim(); existing.Notes = Customer.Notes; existing.IsBlacklisted = Customer.IsBlacklisted; existing.BlacklistReason = Customer.IsBlacklisted ? Customer.BlacklistReason?.Trim() : null;
        try { await _db.SaveChangesAsync(); } catch (Microsoft.Data.SqlClient.SqlException ex) { ModelState.AddModelError(string.Empty, ex.Message.Contains("UQ_CUSTOMERS_CustomerCode") ? "That customer code already exists." : "Could not save the customer."); Customer = await _db.Customers.Include(x => x.Addresses).FirstAsync(x => x.CustomerId == Customer.CustomerId); return Page(); }
        TempData["Success"] = "Customer updated."; return RedirectToPage("Edit", new { id = Customer.CustomerId });
    }

    public async Task<IActionResult> OnPostAddAddressAsync(int customerId, string label, string addressLine, string city, bool isDefault)
    {
        if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(addressLine) || string.IsNullOrWhiteSpace(city))
        { TempData["Error"] = "Label, address line and city are required."; return RedirectToPage("Edit", new { id = customerId }); }
        if (!await _db.Customers.AnyAsync(c => c.CustomerId == customerId)) return NotFound();
        if (isDefault) await _db.CustomerAddresses.Where(a => a.CustomerId == customerId && a.IsDefault).ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false));
        _db.CustomerAddresses.Add(new CustomerAddress { CustomerId = customerId, Label = label.Trim(), AddressLine = addressLine.Trim(), City = city.Trim(), IsDefault = isDefault });
        try { await _db.SaveChangesAsync(); TempData["Success"] = "Address added."; }
        catch (Microsoft.Data.SqlClient.SqlException) { TempData["Error"] = "Could not save the address. Please check the address values."; }
        return RedirectToPage("Edit", new { id = customerId });
    }
}
