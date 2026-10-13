namespace PrintShop3D.Common;

public static class WorkflowRules
{
    private static readonly Dictionary<string, HashSet<string>> OrderTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DRAFT"] = new(StringComparer.OrdinalIgnoreCase) { "CONFIRMED", "CANCELED" },
        ["CONFIRMED"] = new(StringComparer.OrdinalIgnoreCase) { "COMPLETED", "ON_HOLD", "CANCELED" },
        ["ON_HOLD"] = new(StringComparer.OrdinalIgnoreCase) { "CONFIRMED", "CANCELED" },
        ["COMPLETED"] = new(StringComparer.OrdinalIgnoreCase) { "CANCELED" },
        ["CANCELED"] = new(StringComparer.OrdinalIgnoreCase)
    };

    private static readonly Dictionary<string, HashSet<string>> ProductionTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["QUEUED"] = new(StringComparer.OrdinalIgnoreCase) { "SCHEDULED", "ON_HOLD", "FAILED" },
        ["SCHEDULED"] = new(StringComparer.OrdinalIgnoreCase) { "IN_PROGRESS", "QUEUED", "ON_HOLD", "FAILED" },
        ["IN_PROGRESS"] = new(StringComparer.OrdinalIgnoreCase) { "QUALITY_CHECK", "FAILED", "ON_HOLD" },
        ["QUALITY_CHECK"] = new(StringComparer.OrdinalIgnoreCase) { "COMPLETED", "IN_PROGRESS", "FAILED", "ON_HOLD" },
        ["COMPLETED"] = new(StringComparer.OrdinalIgnoreCase),
        ["FAILED"] = new(StringComparer.OrdinalIgnoreCase) { "QUEUED", "SCHEDULED", "ON_HOLD" },
        ["ON_HOLD"] = new(StringComparer.OrdinalIgnoreCase) { "QUEUED", "SCHEDULED", "IN_PROGRESS", "FAILED" }
    };

    public static bool IsValidOrderTransition(string current, string next) =>
        string.Equals(current, next, StringComparison.OrdinalIgnoreCase) ||
        (OrderTransitions.TryGetValue(current, out var allowed) && allowed.Contains(next));

    public static bool IsValidProductionTransition(string current, string next) =>
        string.Equals(current, next, StringComparison.OrdinalIgnoreCase) ||
        (ProductionTransitions.TryGetValue(current, out var allowed) && allowed.Contains(next));

    public static string StatusBadgeClass(string? status) => (status ?? "").Trim().ToUpperInvariant() switch
    {
        "COMPLETED" or "DELIVERED" or "AVAILABLE" or "ACTIVE" or "ACCEPTED" => "text-bg-success",
        "CONFIRMED" or "SCHEDULED" or "IN_PROGRESS" or "BUSY" or "OUT_FOR_DELIVERY" => "text-bg-primary",
        "QUALITY_CHECK" or "ON_HOLD" or "PENDING" or "MAINTENANCE" or "PARTIALLY_RECEIVED" => "text-bg-warning",
        "FAILED" or "CANCELED" or "CANCELLED" or "OUT_OF_SERVICE" or "REJECTED" => "text-bg-danger",
        "DRAFT" or "QUEUED" or "INACTIVE" or "EXPIRED" or "REASSIGNED" => "text-bg-secondary",
        _ => "text-bg-secondary"
    };
}
