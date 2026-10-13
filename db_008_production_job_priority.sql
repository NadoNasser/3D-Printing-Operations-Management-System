/*
    Production job priority + stale-job escalation support.
    Run this once against the existing PrintShop3D database.
    Purchase tables and logic are not touched.
*/

DECLARE @AddedLastUpdatedAt BIT = 0;

IF COL_LENGTH('dbo.PRODUCTION_JOBS', 'Priority') IS NULL
BEGIN
    ALTER TABLE dbo.PRODUCTION_JOBS
        ADD Priority INT NOT NULL
            CONSTRAINT DF_PRODUCTION_JOBS_Priority DEFAULT (2);
END;

IF COL_LENGTH('dbo.PRODUCTION_JOBS', 'LastUpdatedAt') IS NULL
BEGIN
    ALTER TABLE dbo.PRODUCTION_JOBS
        ADD LastUpdatedAt DATETIME2 NOT NULL
            CONSTRAINT DF_PRODUCTION_JOBS_LastUpdatedAt DEFAULT (SYSDATETIME());
    SET @AddedLastUpdatedAt = 1;
END;

/* Existing jobs: only on the first migration, use StartTime as the best
   available historical activity timestamp; otherwise keep the stored value. */
IF @AddedLastUpdatedAt = 1
BEGIN
    UPDATE dbo.PRODUCTION_JOBS
    SET LastUpdatedAt = COALESCE(StartTime, LastUpdatedAt);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = 'CK_PRODUCTION_JOBS_Priority'
      AND parent_object_id = OBJECT_ID('dbo.PRODUCTION_JOBS')
)
BEGIN
    ALTER TABLE dbo.PRODUCTION_JOBS
        ADD CONSTRAINT CK_PRODUCTION_JOBS_Priority
        CHECK (Priority BETWEEN 1 AND 4);
END;
GO

/* Keep the defaults explicit for future inserts. */
IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints
    WHERE name = 'DF_PRODUCTION_JOBS_Priority'
      AND parent_object_id = OBJECT_ID('dbo.PRODUCTION_JOBS')
)
BEGIN
    ALTER TABLE dbo.PRODUCTION_JOBS
        ADD CONSTRAINT DF_PRODUCTION_JOBS_Priority DEFAULT (2) FOR Priority;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints
    WHERE name = 'DF_PRODUCTION_JOBS_LastUpdatedAt'
      AND parent_object_id = OBJECT_ID('dbo.PRODUCTION_JOBS')
)
BEGIN
    ALTER TABLE dbo.PRODUCTION_JOBS
        ADD CONSTRAINT DF_PRODUCTION_JOBS_LastUpdatedAt DEFAULT (SYSDATETIME()) FOR LastUpdatedAt;
END;
GO
