namespace PrintShop3D.Common;

// Single source of truth for every status / enum-like value shown in dropdowns.
// These lists MUST always match the CHECK constraints defined in create_database.sql.
// If the database constraints ever change, update ONLY this file.
public static class DomainConstants
{
    public static readonly string[] EmployeeStatuses = { "ACTIVE", "INACTIVE" };

    public static readonly string[] PrinterTechnologies = { "FDM", "RESIN", "SLA", "MSLA" };
    public static readonly string[] PrinterStatuses =
        { "AVAILABLE", "BUSY", "MAINTENANCE", "OUT_OF_SERVICE", "INACTIVE" };

    public static readonly string[] QuotationStatuses =
        { "DRAFT", "SENT", "ACCEPTED", "EXPIRED", "REJECTED" };

    public static readonly string[] OrderStatuses =
    {
        "DRAFT", "CONFIRMED", "ON_HOLD", "COMPLETED", "CANCELED"
    };

    public static readonly string[] PaymentStatuses = { "PENDING", "COMPLETED", "FAILED", "REFUNDED" };
    public static readonly string[] PaymentMethods = { "CASH", "CARD", "BANK_TRANSFER", "MOBILE_WALLET", "OTHER" };

    public static readonly string[] DeliveryMethods = { "PICKUP", "COURIER", "SHIPPING" };
    public static readonly string[] DeliveryStatuses = { "PENDING", "OUT_FOR_DELIVERY", "DELIVERED", "FAILED" };

    public static readonly string[] ProductionJobStatuses =
        { "QUEUED", "SCHEDULED", "IN_PROGRESS", "QUALITY_CHECK", "COMPLETED", "FAILED", "ON_HOLD" };

    public static readonly int[] ProductionJobPriorities = { 1, 2, 3, 4 };

    public static readonly string[] JobAssignmentRoles = { "PRIMARY", "ASSISTANT" };
    public static readonly string[] JobAssignmentStatuses = { "ASSIGNED", "IN_PROGRESS", "COMPLETED", "REASSIGNED" };

    public static readonly string[] PrinterAssignmentStatuses =
        { "SCHEDULED", "PRINTING", "COMPLETED", "FAILED", "CANCELED" };

    public static readonly string[] PurchaseOrderStatuses =
        { "DRAFT", "SENT", "CONFIRMED", "PARTIALLY_RECEIVED", "RECEIVED", "CANCELED" };

    public static readonly string[] InventoryTransactionTypes =
    { "PURCHASE", "SALE", "ADJUSTMENT", "RETURN", "TRANSFER" };

    public static readonly string[] AssetStatuses = { "ACTIVE", "RETIRED", "DISPOSED" };

    public static readonly string[] MaintenanceTypes = { "PREVENTIVE", "CORRECTIVE", "EMERGENCY" };
    public static readonly string[] MaintenanceStatuses = { "REPORTED", "IN_PROGRESS", "COMPLETED" };

    public static readonly string[] UserRoles = { "Admin", "Staff" };

    // PrintQuality has no DB CHECK constraint (it's free text), but we keep a
    // fixed list here so data entry stays consistent across the app.
    public static readonly string[] PrintQualities = { "High Quality", "Mid Quality", "Low Quality" };

    // Material names used in PRINTERS.BestPrintMaterial JSON and by
    // dbo.usp_SuggestPrinter. Keep this in sync with whatever material set
    // was used when BestPrintMaterial was populated for your printers.
    public static readonly string[] PrintMaterialTypes =
    {
        "PLA", "PETG", "ABS", "PETG HF", "PLA CF", "ABS CF", "TPU", "PACF", "ASA", "PC", "PLA HS"
    };
}
