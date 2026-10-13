/*
    Allows multiple orders without a quotation while preserving uniqueness
    when a real QuotationID is supplied.

    This fixes SQL Server error 2627 caused by a normal UNIQUE constraint on
    nullable ORDERS.QuotationID (SQL Server permits only one NULL in that index).
*/

IF EXISTS (
    SELECT 1
    FROM sys.key_constraints
    WHERE parent_object_id = OBJECT_ID('dbo.ORDERS')
      AND name = 'UQ_ORDERS_QuotationID'
)
BEGIN
    ALTER TABLE dbo.ORDERS DROP CONSTRAINT UQ_ORDERS_QuotationID;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.ORDERS')
      AND name = 'UX_ORDERS_QuotationID'
)
BEGIN
    CREATE UNIQUE INDEX UX_ORDERS_QuotationID
        ON dbo.ORDERS (QuotationID)
        WHERE QuotationID IS NOT NULL;
END;
GO
