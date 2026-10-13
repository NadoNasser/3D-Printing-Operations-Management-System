/* Customer blacklist support. Run once against Production3DPrintDB. */
IF COL_LENGTH('dbo.CUSTOMERS', 'IsBlacklisted') IS NULL
BEGIN
    ALTER TABLE dbo.CUSTOMERS ADD IsBlacklisted BIT NOT NULL CONSTRAINT DF_CUSTOMERS_IsBlacklisted DEFAULT (0);
END;
GO
IF COL_LENGTH('dbo.CUSTOMERS', 'BlacklistReason') IS NULL
BEGIN
    ALTER TABLE dbo.CUSTOMERS ADD BlacklistReason NVARCHAR(500) NULL;
END;
GO
