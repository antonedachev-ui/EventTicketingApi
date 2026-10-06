USE EventTicketing;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    ------------------------------------------------------------
    -- Event 1: Future event, no sales yet
    ------------------------------------------------------------

    DECLARE @Event1Id INT;

    INSERT INTO dbo.[Event]
    (
        [Name],
        [Description],
        Venue,
        EventDateTimeUtc
    )
    VALUES
    (
        N'London Technology Conference',
        N'Annual technology conference for software engineers.',
        N'London ExCeL',
        '2026-11-21T09:00:00'
    );

    SET @Event1Id = SCOPE_IDENTITY();

    INSERT INTO dbo.PricingTier
    (
        EventId,
        [Name],
        Price,
        TotalCapacity,
        AvailableCapacity
    )
    VALUES
        (@Event1Id, N'Standard', 49.99, 200, 200),
        (@Event1Id, N'Premium',  89.99, 100, 100),
        (@Event1Id, N'VIP',     149.99,  25,  25);


    ------------------------------------------------------------
    -- Event 2: Event with existing ticket sales
    ------------------------------------------------------------

    DECLARE @Event2Id INT;

    INSERT INTO dbo.[Event]
    (
        [Name],
        [Description],
        Venue,
        EventDateTimeUtc
    )
    VALUES
    (
        N'Jazz Evening',
        N'Live jazz performance.',
        N'Royal Festival Hall',
        '2026-12-12T19:30:00'
    );

    SET @Event2Id = SCOPE_IDENTITY();

    DECLARE @StandardTierId INT;
    DECLARE @PremiumTierId INT;

    INSERT INTO dbo.PricingTier
    (
        EventId,
        [Name],
        Price,
        TotalCapacity,
        AvailableCapacity
    )
    VALUES
    (
        @Event2Id,
        N'Standard',
        35.00,
        150,
        140       -- 10 already sold
    );

    SET @StandardTierId = SCOPE_IDENTITY();

    INSERT INTO dbo.PricingTier
    (
        EventId,
        [Name],
        Price,
        TotalCapacity,
        AvailableCapacity
    )
    VALUES
    (
        @Event2Id,
        N'Premium',
        65.00,
        50,
        45         -- 5 already sold
    );

    SET @PremiumTierId = SCOPE_IDENTITY();

    INSERT INTO dbo.TicketPurchase
    (
        PricingTierId,
        Quantity,
        UnitPrice,
        PurchasedAtUtc
    )
    VALUES
        (@StandardTierId, 4, 35.00, '2026-10-01T10:15:00'),
        (@StandardTierId, 6, 35.00, '2026-10-03T14:30:00'),
        (@PremiumTierId,  2, 65.00, '2026-10-02T11:00:00'),
        (@PremiumTierId,  3, 65.00, '2026-10-04T16:45:00');


    ------------------------------------------------------------
    -- Event 3: Almost sold out
    ------------------------------------------------------------

    DECLARE @Event3Id INT;

    INSERT INTO dbo.[Event]
    (
        [Name],
        [Description],
        Venue,
        EventDateTimeUtc
    )
    VALUES
    (
        N'Indie Music Night',
        NULL,
        N'Camden Assembly',
        '2026-11-07T20:00:00'
    );

    SET @Event3Id = SCOPE_IDENTITY();

    INSERT INTO dbo.PricingTier
    (
        EventId,
        [Name],
        Price,
        TotalCapacity,
        AvailableCapacity
    )
    VALUES
        (@Event3Id, N'General Admission', 25.00, 100, 3);


    ------------------------------------------------------------
    -- Event 4: Soft-deleted event
    ------------------------------------------------------------

    DECLARE @Event4Id INT;
    DECLARE @DeletionTimestampUtc DATETIME2(0) = SYSUTCDATETIME();

    INSERT INTO dbo.[Event]
    (
        [Name],
        [Description],
        Venue,
        EventDateTimeUtc,
        EventDeletionTimestampUtc
    )
    VALUES
    (
        N'Cancelled Test Event',
        N'Used for testing soft deletion.',
        N'Test Venue',
        '2026-12-20T18:00:00',
        @DeletionTimestampUtc
    );

    SET @Event4Id = SCOPE_IDENTITY();

    INSERT INTO dbo.PricingTier
    (
        EventId,
        [Name],
        Price,
        TotalCapacity,
        AvailableCapacity,
        PricingTierDeletionTimestampUtc
    )
    VALUES
    (
        @Event4Id,
        N'Standard',
        20.00,
        100,
        100,
        @DeletionTimestampUtc
    );


    COMMIT TRANSACTION;


    ------------------------------------------------------------
    -- Display generated IDs
    ------------------------------------------------------------

    SELECT
        @Event1Id AS LondonTechnologyConferenceEventId,
        @Event2Id AS JazzEveningEventId,
        @Event3Id AS IndieMusicNightEventId,
        @Event4Id AS DeletedEventId;

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO