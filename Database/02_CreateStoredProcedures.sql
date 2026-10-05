CREATE OR ALTER PROCEDURE dbo.Ticket_Purchase
    @PricingTierId INT,
    @Quantity INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Check parameters 
    IF @Quantity <= 0 RETURN 3; -- InvalidQuantity    

    DECLARE @PurchaseId BIGINT, @UnitPrice	DECIMAL(10,2);

    BEGIN TRY
        BEGIN TRANSACTION;
        
            UPDATE PT SET PT.AvailableCapacity = PT.AvailableCapacity - @Quantity   -- Atomically decrement capacity only when sufficient inventory remains.
            FROM   dbo.PricingTier AS PT                                            -- The CHECK constraint provides an additional database-level safeguard.
            INNER JOIN dbo.[Event] E ON E.EventId = PT.EventId
            WHERE  PT.PricingTierId = @PricingTierId                            
                   AND PT.PricingTierDeletionTimestampUtc IS NULL                   -- The updated tier remains protected from conflicting modifications
                   AND PT.AvailableCapacity >= @Quantity                            -- until the transaction completes.
                   AND E.EventDeletionTimestampUtc IS NULL;                         -- The event must be "active" here
            
            IF @@ROWCOUNT = 0
            BEGIN

                ROLLBACK TRANSACTION;

                IF NOT EXISTS 
                (
                    SELECT 1 FROM dbo.PricingTier AS P 
                    INNER JOIN dbo.[Event] E ON E.EventId = P.EventId
                    WHERE P.PricingTierId = @PricingTierId 
                    AND P.PricingTierDeletionTimestampUtc IS NULL
                    AND E.EventDeletionTimestampUtc IS NULL
                ) RETURN 1; -- PricingTierNotAvailable 

                RETURN 2; -- InsufficientCapacity                
            END
            

            SELECT @UnitPrice = PT.Price
            FROM dbo.PricingTier AS PT 
            WHERE   PT.PricingTierId = @PricingTierId;

            INSERT dbo.TicketPurchase (PricingTierId, Quantity, UnitPrice) VALUES (@PricingTierId, @Quantity, @UnitPrice)

            SET @PurchaseId = SCOPE_IDENTITY();

        COMMIT TRANSACTION;
    END TRY 
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT
        TP.PurchaseId,
        TP.PricingTierId,
        TP.Quantity,
        TP.UnitPrice,
        TP.PurchasedAtUtc
    FROM dbo.TicketPurchase TP
    WHERE TP.PurchaseId = @PurchaseId;

    RETURN 0; -- Success
END
GO

CREATE OR ALTER PROCEDURE dbo.Event_Create
(
    @Name             NVARCHAR(256),
    @Description      NVARCHAR(512),
    @Venue            NVARCHAR(256),
    @EventDateTimeUtc DATETIME2(0),
    @PricingTiers     dbo.PricingTierInputType READONLY
)
AS 
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF ISNULL(RTRIM(LTRIM(@Name)),'') = '' RETURN 2; -- EventNameInvalid

    IF ISNULL(RTRIM(LTRIM(@Venue)),'') = '' RETURN 3; -- VenueNameInvalid

    IF NOT EXISTS
    (
        SELECT 1 
        FROM @PricingTiers AS P
    ) RETURN 4; -- PricingTierMissing

    IF EXISTS
    (
        SELECT 1
        FROM @PricingTiers AS P
        WHERE ISNULL(LTRIM(RTRIM(P.[Name])), '') = ''
    ) RETURN 5; -- PricingTierNameInvalid

    IF EXISTS 
    (
        SELECT 1 
        FROM @PricingTiers AS P
        GROUP BY P.Name
        HAVING COUNT(P.Name) >1
    ) RETURN 6; -- DuplicatePricingTierNames

    IF EXISTS 
    (
        SELECT 1 FROM @PricingTiers AS P WHERE P.Price < 0
    ) RETURN 7; -- PricingTierInvalidPrice

    IF EXISTS 
    (
        SELECT 1 FROM @PricingTiers AS P WHERE P.TotalCapacity <= 0
    ) RETURN 8; -- PricingTierInvalidTotalCapacity

    IF EXISTS
    (
        SELECT 1 FROM @PricingTiers WHERE PricingTierId IS NOT NULL
    ) RETURN 9; -- InvalidPricingTierId

    DECLARE @EventId INT;

    BEGIN TRY
        BEGIN TRANSACTION;
        INSERT INTO dbo.[Event] ([Name],[Description],Venue,EventDateTimeUtc) VALUES (@Name, @Description, @Venue, @EventDateTimeUtc);

        SET @EventId = SCOPE_IDENTITY();

        INSERT INTO dbo.PricingTier (EventId, [Name], Price, TotalCapacity, AvailableCapacity) 
        SELECT @EventId, P.Name, P.Price, P.TotalCapacity, P.TotalCapacity  FROM @PricingTiers AS P 

        COMMIT TRANSACTION;
    END TRY 
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT E.EventId,
        E.[Name],
        E.[Description],
        E.Venue,
        E.EventDateTimeUtc
    FROM dbo.[Event] AS E
    WHERE E.EventId = @EventId
        AND E.EventDeletionTimestampUtc IS NULL;

    SELECT PT.PricingTierId,
        PT.[Name],
        PT.Price,
        PT.TotalCapacity,
        PT.AvailableCapacity
    FROM dbo.PricingTier AS PT
    WHERE PT.EventId = @EventId
      AND PT.PricingTierDeletionTimestampUtc IS NULL
    ORDER BY PT.PricingTierId;

    RETURN 0; -- Success
END
GO

CREATE OR ALTER PROCEDURE dbo.Event_Get
    @EventId INT
AS 
BEGIN

    SET NOCOUNT ON;    

    DECLARE @CNT INT;

    SELECT E.EventId,
        E.[Name],
        E.[Description],
        E.Venue,
        E.EventDateTimeUtc
    FROM dbo.[Event] AS E
    WHERE E.EventId = @EventId
        AND E.EventDeletionTimestampUtc IS NULL;

    SET @CNT = @@ROWCOUNT;

    SELECT PT.PricingTierId,
        PT.[Name],
        PT.Price,
        PT.TotalCapacity,
        PT.AvailableCapacity
    FROM dbo.PricingTier AS PT
    INNER JOIN dbo.[Event] AS E
        ON E.EventId = PT.EventId
    WHERE PT.EventId = @EventId
      AND PT.PricingTierDeletionTimestampUtc IS NULL
      AND E.EventDeletionTimestampUtc IS NULL;

    IF @CNT = 0 RETURN 1; -- EventNotFound
    RETURN 0; -- Success
END
GO

CREATE OR ALTER PROCEDURE dbo.Event_Update
    @EventId INT,
    @Name             NVARCHAR(256),
    @Description      NVARCHAR(512),
    @Venue            NVARCHAR(256),
    @EventDateTimeUtc DATETIME2(0),
    @PricingTiers     dbo.PricingTierInputType READONLY
AS 
BEGIN
    
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF ISNULL(RTRIM(LTRIM(@Name)),'') = '' RETURN 2; -- EventNameInvalid

    IF ISNULL(RTRIM(LTRIM(@Venue)),'') = '' RETURN 3; -- VenueNameInvalid

    IF NOT EXISTS
    (
        SELECT 1 
        FROM @PricingTiers AS P
    ) RETURN 4; -- PricingTierMissing

    IF EXISTS
    (
        SELECT 1
        FROM @PricingTiers AS P
        WHERE ISNULL(LTRIM(RTRIM(P.[Name])), '') = ''
    ) RETURN 5; -- PricingTierNameInvalid

    IF EXISTS 
    (
        SELECT 1 
        FROM @PricingTiers AS P
        GROUP BY P.Name
        HAVING COUNT(P.Name) >1
    ) RETURN 6; -- DuplicatePricingTierNames

    IF EXISTS 
    (
        SELECT 1 FROM @PricingTiers AS P WHERE P.Price < 0
    ) RETURN 7; -- PricingTierInvalidPrice

    IF EXISTS 
    (
        SELECT 1 FROM @PricingTiers AS P WHERE P.TotalCapacity <= 0
    ) RETURN 8; -- PricingTierInvalidTotalCapacity

    IF EXISTS
    (
        SELECT PricingTierId
        FROM @PricingTiers
        WHERE PricingTierId IS NOT NULL
        GROUP BY PricingTierId
        HAVING COUNT(PricingTierId) > 1
    ) RETURN 9; -- InvalidPricingTierId

    IF EXISTS
    (
        SELECT 1
        FROM @PricingTiers AS PTU
        LEFT JOIN dbo.PricingTier AS PT
           ON PT.PricingTierId = PTU.PricingTierId
           AND PT.EventId = @EventId
           AND PT.PricingTierDeletionTimestampUtc IS NULL
        WHERE PTU.PricingTierId IS NOT NULL
          AND PT.PricingTierId IS NULL
    ) RETURN 9; -- InvalidPricingTierId

    CREATE TABLE #PricingTierForUpdate
    (
        PricingTierId INT NULL,
        [Name] NVARCHAR(256) NOT NULL,
        Price DECIMAL(10,2) NOT NULL,
        TotalCapacity INT NOT NULL,
        SoldQuantity INT NOT NULL DEFAULT (0)
    );

    INSERT INTO #PricingTierForUpdate (PricingTierId, [Name], Price , TotalCapacity )
    SELECT PricingTierId, [Name], Price, TotalCapacity 
    FROM @PricingTiers;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE dbo.[Event] 
        SET [Name] = @Name,
            [Description] = @Description,
            Venue = @Venue,
            EventDateTimeUtc = @EventDateTimeUtc
        WHERE EventId = @EventId AND EventDeletionTimestampUtc IS NULL;

        IF @@ROWCOUNT = 0 
        BEGIN
            ROLLBACK TRANSACTION;
            RETURN 1; -- EventNotFound
        END;

        UPDATE PTU
        SET PTU.SoldQuantity = PT.TotalCapacity - PT.AvailableCapacity
        FROM #PricingTierForUpdate AS PTU
        INNER JOIN dbo.PricingTier AS PT WITH (UPDLOCK, HOLDLOCK)
            ON PTU.PricingTierId = PT.PricingTierId
            AND PT.PricingTierDeletionTimestampUtc IS NULL
        WHERE PTU.PricingTierId IS NOT NULL;

        IF EXISTS (SELECT 1 FROM #PricingTierForUpdate AS PTU WHERE PTU.SoldQuantity > PTU.TotalCapacity) 
        BEGIN
            ROLLBACK TRANSACTION;
            RETURN 8; -- PricingTierInvalidTotalCapacity 
        END

        MERGE dbo.PricingTier WITH (HOLDLOCK) AS T
        USING 
            (SELECT PTU.PricingTierId, PTU.[Name], PTU.Price, PTU.TotalCapacity, PTU.SoldQuantity  FROM #PricingTierForUpdate AS PTU) 
            AS S (PricingTierId, [Name], Price, TotalCapacity, SoldQuantity)
        ON T.PricingTierId = S.PricingTierId 
        WHEN MATCHED AND T.PricingTierDeletionTimestampUtc IS NULL
            THEN UPDATE
            SET T.[Name] = S.[Name],
                T.Price = S.Price,
                T.TotalCapacity = S.TotalCapacity,
                T.AvailableCapacity = S.TotalCapacity - S.SoldQuantity
        WHEN NOT MATCHED BY TARGET
            THEN INSERT (EventId, [Name], Price, TotalCapacity, AvailableCapacity) 
            VALUES (@EventId, S.Name, S.Price, S.TotalCapacity, S.TotalCapacity - S.SoldQuantity)
        WHEN NOT MATCHED BY SOURCE AND T.EventId = @EventId AND T.PricingTierDeletionTimestampUtc IS NULL
            THEN UPDATE SET T.PricingTierDeletionTimestampUtc = SYSUTCDATETIME();

        COMMIT TRANSACTION;
    END TRY 
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT E.EventId,
        E.[Name],
        E.[Description],
        E.Venue,
        E.EventDateTimeUtc
    FROM dbo.[Event] AS E
    WHERE E.EventId = @EventId
        AND E.EventDeletionTimestampUtc IS NULL;

    SELECT PT.PricingTierId,
        PT.[Name],
        PT.Price,
        PT.TotalCapacity,
        PT.AvailableCapacity
    FROM dbo.PricingTier AS PT
    WHERE PT.EventId = @EventId
      AND PT.PricingTierDeletionTimestampUtc IS NULL
    ORDER BY PT.PricingTierId;

    RETURN 0; -- Success
    
END
GO

CREATE OR ALTER PROCEDURE dbo.Event_Delete
    @EventId INT
AS 
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @DeletionTimestampUtc DATETIME2(0) = SYSUTCDATETIME();

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE  E
        SET E.EventDeletionTimestampUtc = @DeletionTimestampUtc
        FROM dbo.[Event] AS E
        WHERE E.EventId = @EventId AND E.EventDeletionTimestampUtc IS NULL;

        IF @@ROWCOUNT = 0 
        BEGIN
            ROLLBACK TRANSACTION;
            RETURN 1; -- EventNotFound
        END;

        UPDATE PT
        SET PT.PricingTierDeletionTimestampUtc = @DeletionTimestampUtc -- @DeletionTimestampUtc here, because dbo.[Event] and dbo.PricingTier should have exactly the same deletion time stamp
        FROM dbo.PricingTier AS PT 
        WHERE PT.EventId = @EventId AND PT.PricingTierDeletionTimestampUtc IS NULL;

    COMMIT TRANSACTION;
    END TRY 
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    RETURN 0; -- Success
END
GO


-- This stored proc can be called by a SQL Server job for events which were "soft" deleted some time ago, for example, a year ago
CREATE OR ALTER PROCEDURE dbo.Event_Purge
    @EventId INT
AS 
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;


        IF NOT EXISTS 
        (SELECT 1 FROM dbo.[Event] AS E WITH (UPDLOCK, HOLDLOCK) WHERE E.EventId = @EventId AND E.EventDeletionTimestampUtc IS NOT NULL)
        BEGIN
            ROLLBACK TRANSACTION;
            RETURN 1;  -- EventNotFound
        END
         
        DELETE FROM dbo.TicketPurchase
        WHERE PricingTierId IN 
            (SELECT PT.PricingTierId FROM dbo.PricingTier AS PT WHERE PT.EventId = @EventId)

        DELETE FROM dbo.PricingTier  WHERE EventId = @EventId;

        DELETE FROM dbo.[Event] WHERE EventId = @EventId;

        COMMIT TRANSACTION;
    END TRY 
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    RETURN 0; -- Success
END
GO

CREATE OR ALTER PROCEDURE dbo.Event_GetAvailability
    @EventId INT
AS 
BEGIN
    SET NOCOUNT ON;

    SELECT  PT.PricingTierId,
            PT.[Name],
            PT.Price,
            PT.TotalCapacity,
            PT.AvailableCapacity
    FROM dbo.PricingTier AS PT
    INNER JOIN dbo.[Event] AS E
    ON PT.EventId = E.EventId
    WHERE PT.PricingTierDeletionTimestampUtc IS NULL
    AND E.EventDeletionTimestampUtc IS NULL
    AND E.EventId = @EventId;

    RETURN 0; -- Success
END
GO

CREATE OR ALTER PROCEDURE dbo.Event_GetSalesSummary
    @EventId INT
AS 
BEGIN
    SET NOCOUNT ON;

    SELECT E.EventId, E.[Name] AS EventName,
        COALESCE(SUM(TP.Quantity), 0) AS TicketsSold,
        COALESCE(SUM(TP.Quantity * TP.UnitPrice), 0) AS TotalRevenue
    FROM dbo.[Event] AS E
    LEFT OUTER JOIN dbo.PricingTier AS PT
        ON PT.EventId = E.EventId
    LEFT OUTER JOIN dbo.TicketPurchase AS TP
        ON TP.PricingTierId = PT.PricingTierId
    WHERE E.EventId = @EventId
    GROUP BY E.EventId, E.[Name];   -- No deletion timestamp predicates. 
                                    -- If an event or pricing tier was soft-deleted, its historical purchases should still contribute to the sales report.
    RETURN 0; -- Success
END
GO