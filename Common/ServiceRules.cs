using PrintShop3D.Models;

namespace PrintShop3D.Common;

public static class ServiceRules
{
    /// <summary>
    /// A "printing service" is a service that produces physical prints and therefore
    /// supports print-file upload + automatic production-job creation on the order page.
    /// Rule: Category or Name contains "print" (e.g. "3D Printing", "Print - FDM").
    /// Change ONLY this method if you want a different rule (e.g. a Services.IsPrinting column).
    /// </summary>
    public static bool IsPrintingService(Service service) =>
        Contains(service.Category, "print") || Contains(service.Name, "print");

    private static bool Contains(string? value, string term) =>
        value != null && value.Contains(term, StringComparison.OrdinalIgnoreCase);
}
