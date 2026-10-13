using Microsoft.AspNetCore.Http;
using PrintShop3D.Models;

namespace PrintShop3D.Pages.Orders;

/// <summary>A NEW order line posted from the Create or Edit page.</summary>
public class OrderLineInput
{
    public string ItemId { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }

    // printing services only
    public bool CreateJob { get; set; }
    public int Priority { get; set; } = 2;
    public decimal EstimatedHours { get; set; } = 1;   // used only when the line has no files
    public List<OrderFileInput> Files { get; set; } = new();
}

public class OrderFileInput
{
    public IFormFile? Upload { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal EstimatedHours { get; set; } = 1;
}

/// <summary>An item that already exists on the order (Edit page).</summary>
public class ExistingItemInput
{
    public int OrderItemId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public bool Remove { get; set; }
    // only for printing items that have no production job yet
    public bool CreateJob { get; set; }
    public int Priority { get; set; } = 2;
    public decimal EstimatedHours { get; set; } = 1;
}

public record LineTemplateModel(List<Product> Products, List<Service> Services);
public record CustomerPickerModel(List<Customer> Customers, int SelectedId);
