# Event Ticketing API

A RESTful API for a simplified event ticketing system, implemented as a take-home software engineering exercise.

The system supports event and pricing-tier management, ticket purchasing, ticket availability, inventory control, and sales reporting.

## Technology

- .NET 10
- ASP.NET Core Web API
- SQL Server
- Microsoft.Data.SqlClient
- NUnit
- Git / GitHub

The application uses SQL Server stored procedures for database access rather than an ORM.

## Architecture

The application structure is:

```text
HTTP / REST
    |
ASP.NET Core Controllers
    |
Data Access
    |
Generic ADO.NET Stored Procedure Executor
    |
SQL Server Stored Procedures
    |
SQL Server
```

The solution deliberately keeps the architecture relatively small and avoids introducing additional frameworks or abstractions unless they provide a clear benefit for the requirements of the exercise.

## API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/events` | Create an event with pricing tiers |
| `GET` | `/api/events` | List active events without pricing tiers; 404 if none exist |
| `GET` | `/api/events/{id}` | Get an event and its pricing tiers |
| `PUT` | `/api/events/{id}` | Update an event and its pricing tiers |
| `DELETE` | `/api/events/{id}` | Soft-delete an event |
| `GET` | `/api/events/{id}/availability` | Get current ticket availability |
| `GET` | `/api/events/{id}/sales-summary` | Get ticket sales and revenue summary |
| `POST` | `/api/ticket-purchases` | Purchase tickets from a pricing tier |

Expected business failures are represented using appropriate HTTP status codes, including `400 Bad Request`, `404 Not Found`, and `409 Conflict`. Unexpected failures are returned as server errors using ASP.NET Core Problem Details.

## Database Design

The main entities are:

### Event

Represents an event and contains its name, description, venue, date/time, and deletion status.

Events use soft deletion so that historical ticket sales can be retained.

### PricingTier

Represents a ticket pricing tier belonging to an event.

Each pricing tier contains:

- Name
- Price
- Total capacity
- Available capacity

Total event capacity is derived from the capacities of its pricing tiers rather than stored separately.

Pricing tiers are also soft-deleted.

### TicketPurchase

Represents a completed ticket purchase.

Each purchase records:

- Pricing tier
- Quantity
- Unit price at the time of purchase
- Purchase timestamp

The purchase-time unit price is stored so that historical sales reporting remains correct if the current pricing-tier price is subsequently changed.

## Ticket Inventory and Concurrency

Preventing overselling is handled atomically in SQL Server.

A purchase conditionally updates the pricing tier only when sufficient capacity remains:

```sql
UPDATE ...
SET AvailableCapacity = AvailableCapacity - @Quantity
WHERE AvailableCapacity >= @Quantity;
```

The affected-row count determines whether the purchase can proceed.

The availability update and creation of the corresponding `TicketPurchase` record are performed in the same database transaction.

Event/pricing-tier updates use appropriate SQL Server locking to prevent concurrent administrative changes from overwriting ticket inventory changes.

Database constraints provide an additional integrity safeguard.

## Event Updates

An event update supplies the desired set of pricing tiers.

`PricingTierId` identifies an existing tier. A NULL `PricingTierId` represents a new tier.

This allows properties such as the pricing-tier name, price and capacity to change while retaining the identity of an existing tier and its relationship with historical purchases.

Reducing a tier's capacity below the number of tickets already sold is rejected.

Pricing tiers omitted from an update are soft-deleted.

## Deletion and Retention

The normal event delete operation is a soft delete.

Deleting an event also soft-deletes its active pricing tiers while retaining historical ticket-purchase information.

A separate `Event_Purge` database operation provides physical removal of a previously deleted event and its associated data. It is intended as an administrative/data-retention mechanism rather than the normal API delete operation.

In a production system, use of this operation would be restricted and governed by the applicable data-retention policy.

## Availability

Availability is returned for active pricing tiers of active events.

Availability information is a point-in-time view. The authoritative capacity check occurs during the ticket-purchase transaction, so a successful availability query does not itself reserve tickets.

## Sales Reporting

Sales reporting uses the quantity and unit price recorded in `TicketPurchase`.

This preserves historical sales values even if event pricing is changed later.

Soft-deleted events and pricing tiers remain available for historical reporting until they are physically purged.

## Error Handling

Stored procedures follow the convention:

- Result sets contain returned data.
- Integer return values represent expected business outcomes where applicable.
- SQL exceptions represent unexpected database/technical failures.

The .NET application maps database outcomes to application/API results.

User-facing error messages are not stored in the database.

The API returns generic Problem Details for unhandled exceptions. Production-grade error logging and alerting were outside the scope of this exercise. A production system should log technical failures with useful request context while keeping SQL details and secrets out of HTTP responses.

Stored procedures roll back and rethrow unexpected SQL failures. There is no deadlock-specific handling in the database scripts and no application retry policy for deadlock victims, so a deadlock is treated as an unexpected error. A production system should define a bounded retry strategy that is safe for ticket purchases.

## Security

Authentication and authorization are currently outside the scope of this exercise.

In a production system, administrative event-management operations would require appropriate authorization, while public event and availability endpoints could remain anonymous as required.

Database access can be restricted to execution of the application's stored procedures rather than granting the application account direct table modification permissions.

## Testing

The project uses NUnit and contains both unit and integration tests.

Unit tests exercise API/application behaviour in isolation.

Integration tests exercise database-dependent behaviour against SQL Server, including ticket purchasing and the concurrency behaviour used to prevent overselling.

### Running tests

From the repository root, run the unit tests without SQL Server:

```powershell
dotnet test EventTicketing.Tests/EventTicketing.Tests.csproj --filter "TestCategory=Unit"
```

For a clean SQL Server setup, run the scripts in the `Database` directory in this order:

1. `00_CreateDatabase.sql`
2. `01_CreateTables.sql`
3. `02_CreateStoredProcedures.sql`
4. `03_DBPermissions.sql` (when using the restricted application login; create that login separately first)

`04_InsertTestData.sql` is optional sample data. Integration tests arrange their own data and do not require it.

Integration tests require a dedicated SQL Server `EventTicketing` database prepared as above. Set the connection string in the same PowerShell session that runs the tests:

```powershell
$env:EVENT_TICKETING_TEST_CONNECTION_STRING = '<test database connection string>'
dotnet test EventTicketing.Tests/EventTicketing.Tests.csproj --filter "TestCategory=Integration"
```

The integration tests read this environment variable directly; values in .NET user secrets are not read. When the variable is unset, the integration tests are skipped. The tests create their own uniquely named data and do not use `04_InsertTestData.sql`. Teardown soft-deletes their events, leaving historical rows in the test database.

## Running the Project

### Prerequisites

- .NET 10 SDK
- SQL Server

### Database setup

Run the scripts in the `Database` directory in order:

1. `00_CreateDatabase.sql`
2. `01_CreateTables.sql`
3. `02_CreateStoredProcedures.sql`
4. `03_DBPermissions.sql` if using the restricted application login; create that login separately before running this script.

`04_InsertTestData.sql` is optional and inserts sample development data.

### Connection string

For local development, configure the `EventTicketing` connection string using .NET User Secrets rather than committing credentials to the repository. User Secrets are loaded by default when the API runs in the `Development` environment; configure the connection string separately for other environments.

For example, from the API project directory:

```powershell
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:EventTicketing" "<connection string>"
```

### Start the API

Run the API project from Visual Studio, or use `dotnet run` from the API project directory.

The API can then be exercised using the included `EventTicketing.http` file or another HTTP client.

## Design Scope

This project is intentionally scoped as a small take-home exercise rather than a production ticketing platform.

Areas that would require further consideration in a production system include:

- Authentication and authorization
- Payment processing
- Purchase cancellation and refunds
- Reservation/temporary ticket holds
- Idempotency of purchase requests
- Production-grade error logging, alerting, and operational monitoring
- Deadlock handling and safe retry strategy
- Rate limiting
- Deployment and database migration strategy
- Data-retention policy
- Large-scale/high-contention inventory strategies

## AI Assistance

AI tools were used during development as a review and development aid, including discussion of design trade-offs, concurrency behaviour, code review, test development, and documentation.

AI-assisted changes were reviewed, tested, and understood by the author.
