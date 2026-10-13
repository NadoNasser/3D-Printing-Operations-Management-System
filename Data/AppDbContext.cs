using Microsoft.EntityFrameworkCore;
using PrintShop3D.Models;

namespace PrintShop3D.Data;

// Database-First context: maps to the existing Production3DPrintDB schema
// defined in create_database.sql. This context does NOT own migrations for
// the core tables - the database is the source of truth. The only table not
// present in create_database.sql is AppUser (USERS), added separately for
// login purposes (see db/002_add_users_table.sql).
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<EmployeeSkill> EmployeeSkills => Set<EmployeeSkill>();

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<MaterialStockOnHand> MaterialStockOnHand => Set<MaterialStockOnHand>();

    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Printer> Printers => Set<Printer>();
    public DbSet<PrinterMaterialCompatibility> PrinterMaterialCompatibilities => Set<PrinterMaterialCompatibility>();
    public DbSet<AssetMaintenance> AssetMaintenances => Set<AssetMaintenance>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptItem> GoodsReceiptItems => Set<GoodsReceiptItem>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationItem> QuotationItems => Set<QuotationItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();

    public DbSet<ProductionJob> ProductionJobs => Set<ProductionJob>();
    public DbSet<JobAssignment> JobAssignments => Set<JobAssignment>();
    public DbSet<PrinterAssignment> PrinterAssignments => Set<PrinterAssignment>();
    public DbSet<MaterialUsage> MaterialUsages => Set<MaterialUsage>();
    public DbSet<EmployeeEvaluation> EmployeeEvaluations => Set<EmployeeEvaluation>();
    public DbSet<PayrollAdjustment> PayrollAdjustments => Set<PayrollAdjustment>();
    public DbSet<JobCostBreakdown> JobCostBreakdowns => Set<JobCostBreakdown>();

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<PrintFile> PrintFiles => Set<PrintFile>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Customer>(e =>
        {
            e.ToTable("CUSTOMERS");
            e.HasKey(x => x.CustomerId);
            e.Property(x => x.CustomerId).HasColumnName("CustomerID");
            e.HasMany(x => x.Addresses).WithOne(a => a.Customer!).HasForeignKey(a => a.CustomerId);
        });

        b.Entity<CustomerAddress>(e =>
        {
            e.ToTable("CUSTOMER_ADDRESSES");
            e.HasIndex(x => new { x.CustomerId, x.IsDefault }).HasFilter("[IsDefault] = 1").IsUnique();
            e.HasKey(x => x.AddressId);
            e.Property(x => x.AddressId).HasColumnName("AddressID");
            e.Property(x => x.CustomerId).HasColumnName("CustomerID");
        });

        b.Entity<Employee>(e =>
        {
            e.ToTable("EMPLOYEES");
            e.HasKey(x => x.EmployeeId);
            e.Property(x => x.EmployeeId).HasColumnName("EmployeeID");
            e.Property(x => x.HourlyRate).HasPrecision(18, 2);
        });

        b.Entity<Skill>(e =>
        {
            e.ToTable("SKILLS");
            e.HasKey(x => x.SkillId);
            e.Property(x => x.SkillId).HasColumnName("SkillID");
        });

        b.Entity<EmployeeSkill>(e =>
        {
            e.ToTable("EMPLOYEE_SKILLS");
            e.HasKey(x => x.EmployeeSkillId);
            e.Property(x => x.EmployeeSkillId).HasColumnName("EmployeeSkillID");
            e.Property(x => x.EmployeeId).HasColumnName("EmployeeID");
            e.Property(x => x.SkillId).HasColumnName("SkillID");
        });

        b.Entity<Product>(e =>
        {
            e.ToTable("PRODUCTS");
            e.HasKey(x => x.ProductId);
            e.Property(x => x.ProductId).HasColumnName("ProductID");
            e.Property(x => x.BasePrice).HasPrecision(18, 2);
            e.ToTable("PRODUCTS");
            e.HasKey(x => x.ProductId);

            e.Property(x => x.ProductId)
                .HasColumnName("ProductID");

            e.Property(x => x.MaterialId)
                .HasColumnName("MaterialID");

            e.Property(x => x.BasePrice)
                .HasPrecision(18, 2);

            e.HasOne(x => x.Material)
                .WithMany()
                .HasForeignKey(x => x.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Service>(e =>
        {
            e.ToTable("SERVICES");
            e.HasKey(x => x.ServiceId);
            e.Property(x => x.ServiceId).HasColumnName("ServiceID");
            e.Property(x => x.BaseRate).HasPrecision(18, 2);
        });

        b.Entity<Material>(e =>
        {
            e.ToTable("MATERIALS");
            e.HasKey(x => x.MaterialId);
            e.Property(x => x.MaterialId).HasColumnName("MaterialID");
            e.Property(x => x.Diameter).HasPrecision(18, 3);
            e.Property(x => x.PurchasePrice).HasPrecision(18, 2);
            e.Property(x => x.SalePrice).HasPrecision(18, 2);
            e.Property(x => x.SafetyStock).HasPrecision(18, 3);
            e.Property(x => x.ReorderLevel).HasPrecision(18, 3);
        });

        b.Entity<MaterialStockOnHand>(e =>
        {
            e.HasNoKey();
            e.ToView("VW_MATERIAL_STOCK_ON_HAND");
            e.Property(x => x.MaterialId).HasColumnName("MaterialID");
            e.Property(x => x.SafetyStock).HasPrecision(18, 3);
            e.Property(x => x.ReorderLevel).HasPrecision(18, 3);
            e.Property(x => x.CurrentStockLevel).HasPrecision(18, 3);
        });

        b.Entity<Asset>(e =>
        {
            e.ToTable("ASSETS");
            e.HasKey(x => x.AssetId);
            e.Property(x => x.AssetId).HasColumnName("AssetID");
            e.Property(x => x.PurchaseCost).HasPrecision(18, 2);
            e.Property(x => x.CurrentValue).HasPrecision(18, 2);
        });

        b.Entity<Printer>(e =>
        {
            e.ToTable("PRINTERS");
            e.HasKey(x => x.PrinterId);
            e.Property(x => x.PrinterId).HasColumnName("PrinterID");
            e.Property(x => x.AssetId).HasColumnName("AssetID");
            e.Property(x => x.BestPrintMaterial).HasColumnType("nvarchar(max)");
            e.Property(x => x.BuildVolumeX).HasPrecision(10, 2);
            e.Property(x => x.BuildVolumeY).HasPrecision(10, 2);
            e.Property(x => x.BuildVolumeZ).HasPrecision(10, 2);
            e.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId);
        });

        b.Entity<PrinterMaterialCompatibility>(e =>
        {
            e.ToTable("PRINTER_MATERIAL_COMPATIBILITY");
            e.HasKey(x => x.CompatibilityId);
            e.Property(x => x.CompatibilityId).HasColumnName("CompatibilityID");
            e.Property(x => x.PrinterId).HasColumnName("PrinterID");
            e.Property(x => x.MaterialId).HasColumnName("MaterialID");
        });

        b.Entity<AssetMaintenance>(e =>
        {
            e.ToTable("ASSET_MAINTENANCE");
            e.HasKey(x => x.MaintenanceId);
            e.Property(x => x.MaintenanceId).HasColumnName("MaintenanceID");
            e.Property(x => x.AssetId).HasColumnName("AssetID");
            e.Property(x => x.TechnicianId).HasColumnName("TechnicianID");
            e.Property(x => x.Cost).HasPrecision(18, 2);
        });

        b.Entity<Supplier>(e =>
        {
            e.ToTable("SUPPLIERS");
            e.HasKey(x => x.SupplierId);
            e.Property(x => x.SupplierId).HasColumnName("SupplierID");
        });

        b.Entity<PurchaseOrder>(e =>
        {
            e.ToTable("PURCHASE_ORDERS");
            e.HasKey(x => x.PurchaseOrderId);
            e.Property(x => x.PurchaseOrderId).HasColumnName("PurchaseOrderID");
            e.Property(x => x.SupplierId).HasColumnName("SupplierID");
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.PurchaseOrderId);
        });

        b.Entity<PurchaseItem>(e =>
        {
            e.ToTable("PURCHASE_ITEMS");
            e.HasKey(x => x.PurchaseItemId);
            e.Property(x => x.PurchaseItemId).HasColumnName("PurchaseItemID");
            e.Property(x => x.PurchaseOrderId).HasColumnName("PurchaseOrderID");
            e.Property(x => x.MaterialId).HasColumnName("MaterialID");
            e.Property(x => x.QuantityOrdered).HasPrecision(18, 3);
            e.Property(x => x.UnitCost).HasPrecision(18, 2);
            e.Property(x => x.LineTotal).HasPrecision(18, 2).ValueGeneratedOnAddOrUpdate();
            e.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId);
        });

        b.Entity<GoodsReceipt>(e =>
        {
            e.ToTable("GOODS_RECEIPTS");
            e.HasKey(x => x.ReceiptId);
            e.Property(x => x.ReceiptId).HasColumnName("ReceiptID");
            e.Property(x => x.PurchaseOrderId).HasColumnName("PurchaseOrderID");
            e.Property(x => x.ReceivedBy).HasColumnName("ReceivedBy");
            e.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.ReceiptId);
        });

        b.Entity<GoodsReceiptItem>(e =>
        {
            e.ToTable("GOODS_RECEIPT_ITEMS");
            e.HasKey(x => x.GoodsReceiptItemId);
            e.Property(x => x.GoodsReceiptItemId).HasColumnName("GoodsReceiptItemID");
            e.Property(x => x.ReceiptId).HasColumnName("ReceiptID");
            e.Property(x => x.PurchaseItemId).HasColumnName("PurchaseItemID");
            e.Property(x => x.QuantityReceived).HasPrecision(18, 3);
            e.Property(x => x.UnitCost).HasPrecision(18, 2);
        });

        b.Entity<InventoryTransaction>(e =>
        {
            e.ToTable("INVENTORY_TRANSACTIONS");

            e.HasKey(x => x.TransactionId);

            e.Property(x => x.TransactionId)
                .HasColumnName("TransactionID");

            e.Property(x => x.MaterialId)
                .HasColumnName("MaterialID");

            e.Property(x => x.Quantity)
                .HasPrecision(18, 3);

            e.Property(x => x.UnitCost)
                .HasPrecision(18, 2);

            e.Property(x => x.GoodsReceiptItemId)
                .HasColumnName("GoodsReceiptItemID");

            e.Property(x => x.MaterialUsageId)
                .HasColumnName("MaterialUsageID");

            e.Property(x => x.OrderItemId)
                .HasColumnName("OrderItemID");

            e.HasOne(x => x.Material)
                .WithMany()
                .HasForeignKey(x => x.MaterialId);

            e.HasOne(x => x.OrderItem)
                .WithMany()
                .HasForeignKey(x => x.OrderItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Quotation>(e =>
        {
            e.ToTable("QUOTATIONS");
            e.HasKey(x => x.QuotationId);
            e.Property(x => x.QuotationId).HasColumnName("QuotationID");
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.CustomerId).HasColumnName("CustomerID");
        });

        b.Entity<QuotationItem>(e =>
        {
            e.ToTable("QUOTATION_ITEMS");
            e.HasKey(x => x.QuotationItemId);
            e.Property(x => x.QuotationItemId).HasColumnName("QuotationItemID");
            e.Property(x => x.QuotationId).HasColumnName("QuotationID");
            e.Property(x => x.ProductId).HasColumnName("ProductID");
            e.Property(x => x.ServiceId).HasColumnName("ServiceID");
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Property(x => x.LineTotal).ValueGeneratedOnAddOrUpdate();
        });

        b.Entity<Order>(e =>
        {
            e.ToTable("ORDERS", tb => tb.HasTrigger("trg_Orders_LogStatusHistory"));
            e.HasKey(x => x.OrderId);
            e.Property(x => x.OrderId).HasColumnName("OrderID");
            e.Property(x => x.CustomerId).HasColumnName("CustomerID");
            e.Property(x => x.QuotationId).HasColumnName("QuotationID");
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.OrderId);
            e.HasMany(x => x.Payments).WithOne(p => p.Order!).HasForeignKey(p => p.OrderId);
            e.HasMany(x => x.Deliveries).WithOne().HasForeignKey(d => d.OrderId);
            e.HasMany(x => x.StatusHistory).WithOne().HasForeignKey(h => h.OrderId);
        });

        b.Entity<OrderItem>(e =>
        {
            e.ToTable("ORDER_ITEMS");
            e.HasKey(x => x.OrderItemId);
            e.Property(x => x.OrderItemId).HasColumnName("OrderItemID");
            e.Property(x => x.OrderId).HasColumnName("OrderID");
            e.Property(x => x.QuotationItemId).HasColumnName("QuotationItemID");
            e.Property(x => x.ProductId).HasColumnName("ProductID");
            e.Property(x => x.ServiceId).HasColumnName("ServiceID");
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Property(x => x.LineTotal).ValueGeneratedOnAddOrUpdate();
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
            e.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId);
            e.HasMany(x => x.Files).WithOne(f => f.OrderItem!).HasForeignKey(f => f.OrderItemId);
            // Inverse side (ProductionJob -> OrderItem) is configured once, below.
        });

        b.Entity<Payment>(e =>
        {
            e.ToTable("PAYMENTS", tb => tb.HasTrigger("TRG_Prevent_Payment_Overage"));
            e.HasKey(x => x.PaymentId);
            e.Property(x => x.PaymentId).HasColumnName("PaymentID");
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.OrderId).HasColumnName("OrderID");
        });

        b.Entity<Delivery>(e =>
        {
            e.ToTable("DELIVERIES");
            e.HasKey(x => x.DeliveryId);
            e.Property(x => x.DeliveryId).HasColumnName("DeliveryID");
            e.Property(x => x.OrderId).HasColumnName("OrderID");
            e.Property(x => x.AddressId).HasColumnName("AddressID");
            e.HasOne(x => x.Address).WithMany().HasForeignKey(x => x.AddressId);
        });

        b.Entity<OrderStatusHistory>(e =>
        {
            e.ToTable("ORDER_STATUS_HISTORY");
            e.HasKey(x => x.HistoryId);
            e.Property(x => x.HistoryId).HasColumnName("HistoryID");
            e.Property(x => x.OrderId).HasColumnName("OrderID");
            e.Property(x => x.ChangedBy).HasColumnName("ChangedBy");
            e.HasOne(x => x.ChangedByEmployee).WithMany().HasForeignKey(x => x.ChangedBy);
        });

        b.Entity<ProductionJob>(e =>
        {
            e.ToTable("PRODUCTION_JOBS");
            e.HasKey(x => x.JobId);
            e.Property(x => x.JobId).HasColumnName("JobID");
            e.Property(x => x.OrderItemId).HasColumnName("OrderItemID");
            e.Property(x => x.EstimatedTime).HasPrecision(10, 2);
            e.Property(x => x.ActualTime).HasPrecision(10, 2);
            e.Property(x => x.Priority).HasColumnName("Priority").HasDefaultValue(2);
            e.Property(x => x.LastUpdatedAt).HasColumnName("LastUpdatedAt").HasDefaultValueSql("SYSDATETIME()");
            e.HasOne(x => x.OrderItem).WithMany(oi => oi.ProductionJobs).HasForeignKey(x => x.OrderItemId);
            e.HasMany(x => x.JobAssignments).WithOne().HasForeignKey(a => a.JobId);
            e.HasMany(x => x.PrinterAssignments).WithOne().HasForeignKey(a => a.JobId);
            e.HasMany(x => x.MaterialUsages).WithOne().HasForeignKey(m => m.JobId);
            e.HasOne(x => x.CostBreakdown).WithOne().HasForeignKey<JobCostBreakdown>(c => c.JobId);
        });

        b.Entity<JobAssignment>(e =>
        {
            e.ToTable("JOB_ASSIGNMENTS", tb => tb.HasTrigger("TRG_Prevent_EmployeeAssignment_Overlap"));
            e.HasKey(x => x.JobAssignmentId);
            e.Property(x => x.JobAssignmentId).HasColumnName("JobAssignmentID");
            e.Property(x => x.JobId).HasColumnName("JobID");
            e.Property(x => x.EmployeeId).HasColumnName("EmployeeID");
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
        });

        b.Entity<PrinterAssignment>(e =>
        {
            e.ToTable("PRINTER_ASSIGNMENTS", tb => tb.HasTrigger("TRG_Prevent_PrinterAssignment_Overlap"));
            e.HasKey(x => x.PrinterAssignmentId);
            e.Property(x => x.PrinterAssignmentId).HasColumnName("PrinterAssignmentID");
            e.Property(x => x.JobId).HasColumnName("JobID");
            e.Property(x => x.PrinterId).HasColumnName("PrinterID");
            e.Property(x => x.AssignedStart).HasColumnName("AssignedStart");
            e.Property(x => x.AssignedEnd).HasColumnName("AssignedEnd");
            e.HasOne(x => x.Printer).WithMany().HasForeignKey(x => x.PrinterId);
        });

        b.Entity<MaterialUsage>(e =>
        {
            e.ToTable("MATERIAL_USAGE");
            e.HasKey(x => x.MaterialUsageId);
            e.Property(x => x.MaterialUsageId).HasColumnName("MaterialUsageID");
            e.Property(x => x.JobId).HasColumnName("JobID");
            e.Property(x => x.MaterialId).HasColumnName("MaterialID");
            e.Property(x => x.QuantityUsed).HasPrecision(18, 3);
            e.Property(x => x.WasteQuantity).HasPrecision(18, 3);
            e.Property(x => x.UnitCost).HasPrecision(18, 2);
            e.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId);
        });

        b.Entity<EmployeeEvaluation>(e =>
        {
            e.ToTable("EMPLOYEE_EVALUATIONS");
            e.HasKey(x => x.EvaluationId);
            e.Property(x => x.EvaluationId).HasColumnName("EvaluationID");
        });

        b.Entity<PayrollAdjustment>(e =>
        {
            e.ToTable("PAYROLL_ADJUSTMENTS");
            e.HasKey(x => x.AdjustmentId);
            e.Property(x => x.AdjustmentId).HasColumnName("AdjustmentID");
            e.Property(x => x.Amount).HasPrecision(18, 2);
        });

        b.Entity<JobCostBreakdown>(e =>
        {
            e.ToTable("JOB_COST_BREAKDOWN");
            e.HasKey(x => x.BreakdownId);
            e.Property(x => x.BreakdownId).HasColumnName("BreakdownID");
            e.Property(x => x.JobId).HasColumnName("JobID");
            e.Property(x => x.MaterialCost).HasPrecision(18, 2);
            e.Property(x => x.MachineCost).HasPrecision(18, 2);
            e.Property(x => x.LaborCost).HasPrecision(18, 2);
            e.Property(x => x.DesignCost).HasPrecision(18, 2);
            e.Property(x => x.PostProcessingCost).HasPrecision(18, 2);
            e.Property(x => x.OutsourcingCost).HasPrecision(18, 2);
            e.Property(x => x.DeliveryCost).HasPrecision(18, 2);
            e.Property(x => x.ProfitMarginPercent).HasPrecision(5, 2);
            e.Property(x => x.FinalPrice).HasPrecision(18, 2).ValueGeneratedOnAddOrUpdate();
        });

        // Additive table (see db/002_add_users_table.sql) - not part of create_database.sql.
        b.Entity<AppUser>(e =>
        {
            e.ToTable("USERS");
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).HasColumnName("UserID");
            e.HasIndex(x => x.Username).IsUnique();
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
        });

        // PRINT_FILES - added by migration 004, linked to ORDER_ITEMS.
        b.Entity<PrintFile>(e =>
        {
            // PRINT_FILES has an enabled trigger in the database. EF Core must be told so it uses
        // "OUTPUT INTO" instead of "OUTPUT" (SQL Server error 334). The name is only a declaration
        // for EF; replace it with the real trigger name from sys.triggers if you like.
        e.ToTable("PRINT_FILES", tb => tb.HasTrigger("TRG_PrintFiles"));
            e.HasKey(x => x.FileId);
            e.Property(x => x.FileId).HasColumnName("FileID");
            e.Property(x => x.OrderItemId).HasColumnName("OrderItemID");
            e.Property(x => x.UploadedBy).HasColumnName("UploadedBy");
            e.HasOne(x => x.UploadedByEmployee).WithMany().HasForeignKey(x => x.UploadedBy);
        });
    }
}
