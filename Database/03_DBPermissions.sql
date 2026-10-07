USE EventTicketing;


GO
/*
    The EventTicketingApp server login must already exist.

    Example for local development:

    CREATE LOGIN EventTicketingApp
        WITH PASSWORD = '<local-development-password>';
*/
IF NOT EXISTS (SELECT 1
               FROM   sys.database_principals
               WHERE  [name] = N'EventTicketingService'
                      AND [type] = 'R')
    BEGIN
        CREATE ROLE EventTicketingService;
    END


GO
IF NOT EXISTS (SELECT 1
               FROM   sys.database_principals
               WHERE  [name] = N'EventTicketingApp'
                      AND [type] IN (
                          'S',
                          'U'
                      ))
    BEGIN
        CREATE USER EventTicketingApp FOR LOGIN EventTicketingApp;
    END


GO
ALTER ROLE EventTicketingService ADD MEMBER EventTicketingApp;


GO
GRANT EXECUTE, REFERENCES
    ON TYPE::dbo.PricingTierInputType TO EventTicketingService;

GO
GRANT EXECUTE
    ON dbo.Event_Create TO EventTicketingService;

GRANT EXECUTE
    ON dbo.Event_Get TO EventTicketingService;

GRANT EXECUTE
    ON dbo.Event_GetAll TO EventTicketingService;

GRANT EXECUTE
    ON dbo.Event_Update TO EventTicketingService;

GRANT EXECUTE
    ON dbo.Event_Delete TO EventTicketingService;

GRANT EXECUTE
    ON dbo.Event_GetAvailability TO EventTicketingService;

GRANT EXECUTE
    ON dbo.Event_GetSalesSummary TO EventTicketingService;

GRANT EXECUTE
    ON dbo.Ticket_Purchase TO EventTicketingService;


GO