use EventTicketing;
GO

IF NOT EXISTS ( SELECT 1  FROM sys.objects  WHERE object_id = OBJECT_ID(N'dbo.[Event]') AND type = 'U')
BEGIN
	CREATE TABLE dbo.[Event] 
	(
		EventId						INT IDENTITY(1,1) NOT NULL,
		[Name]						NVARCHAR (256) NOT NULL,
		[Description]				NVARCHAR (512) NULL,
		Venue						NVARCHAR (256) NOT NULL,
		EventDateTimeUtc			DATETIME2(0) NOT NULL,
		EventDeletionTimestampUtc	DATETIME2(0) NULL,
		CONSTRAINT PK_Event PRIMARY KEY CLUSTERED (EventId)
	);
END
GO


IF NOT EXISTS ( SELECT 1  FROM sys.objects  WHERE object_id = OBJECT_ID(N'dbo.PricingTier') AND type = 'U')
BEGIN
	CREATE TABLE dbo.PricingTier
	(
		PricingTierId					INT IDENTITY(1,1) NOT NULL,
		EventId							INT NOT NULL,
		[Name]							NVARCHAR (256) NOT NULL,
		Price							DECIMAL(10,2) NOT NULL,
		TotalCapacity					INT NOT NULL,
		AvailableCapacity				INT NOT NULL,
		PricingTierDeletionTimestampUtc	DATETIME2(0) NULL,
		CONSTRAINT PK_PricingTier PRIMARY KEY CLUSTERED (PricingTierId),
		CONSTRAINT FK_PricingTier_EventId_Event_EventId FOREIGN KEY (EventId) REFERENCES dbo.[Event](EventId),
		CONSTRAINT CK_PricingTier_Price CHECK (Price >= 0), -- The ticket price may be zero, but it must not be negative
		CONSTRAINT CK_PricingTier_TotalCapacity CHECK (TotalCapacity > 0), -- There's little point creating a pricing tier with zero capacity.
		CONSTRAINT CK_PricingTier_AvailableCapacity CHECK (AvailableCapacity >= 0 AND AvailableCapacity <= TotalCapacity)
	);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PricingTier') AND name = N'UX_PricingTier_EventId_Name_Active')
BEGIN
	CREATE UNIQUE INDEX UX_PricingTier_EventId_Name_Active ON dbo.PricingTier(EventId, [Name]) WHERE PricingTierDeletionTimestampUtc IS NULL;
END
GO

IF NOT EXISTS ( SELECT 1  FROM sys.objects  WHERE object_id = OBJECT_ID(N'dbo.TicketPurchase') AND type = 'U')
BEGIN
	CREATE TABLE dbo.TicketPurchase
	(
		PurchaseId		BIGINT IDENTITY(1,1) NOT NULL,
		PricingTierId	INT NOT NULL,
		Quantity		INT NOT NULL,
		UnitPrice		DECIMAL(10,2) NOT NULL,
		PurchasedAtUtc	DATETIME2(0) NOT NULL CONSTRAINT DF_TicketPurchase_PurchasedAtUtc DEFAULT SYSUTCDATETIME(),
		CONSTRAINT PK_TicketPurchase PRIMARY KEY CLUSTERED (PurchaseId),
		CONSTRAINT FK_TicketPurchase_PricingTierId_PricingTier_PricingTierId FOREIGN KEY (PricingTierId) REFERENCES dbo.PricingTier(PricingTierId),
		CONSTRAINT CK_TicketPurchase_Quantity CHECK (Quantity > 0),
		CONSTRAINT CK_TicketPurchase_UnitPrice CHECK (UnitPrice >= 0)
	)
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TicketPurchase') AND name = N'IX_TicketPurchase_PricingTierId')
BEGIN
    CREATE INDEX IX_TicketPurchase_PricingTierId ON dbo.TicketPurchase(PricingTierId) INCLUDE (Quantity, UnitPrice);
END
GO

IF TYPE_ID(N'dbo.PricingTierInputType') IS NULL
BEGIN
    EXEC
    (	-- The dynamic SQL is necessary here because CREATE TYPE has some batch/DDL restrictions
        'CREATE TYPE dbo.PricingTierInputType AS TABLE
        (
			PricingTierId INT NULL,
            [Name]        NVARCHAR(256) NOT NULL,
            Price         DECIMAL(10,2) NOT NULL,
            TotalCapacity INT NOT NULL
        );'
    );
END
GO