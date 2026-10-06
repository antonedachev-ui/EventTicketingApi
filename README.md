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

## Current Architecture

The intended application structure is:

```text
HTTP / REST
    |
ASP.NET Core Controllers
    |
Application / Service Layer
    |
ADO.NET Database Access
    |
SQL Server Stored Procedures
    |
SQL Server
```

The solution deliberately keeps the architecture relatively small and avoids introducing additional frameworks or abstractions unless they provide a clear benefit for the requirements of the exercise.

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

## Security

Authentication and authorization are currently outside the scope of this exercise.

In a production system, administrative event-management operations would require appropriate authorization, while public event and availability endpoints could remain anonymous as required.

Database access can be restricted to execution of the application's stored procedures rather than granting the application account direct table modification permissions.

## Testing

The project will use NUnit.

Tests are intended to cover both normal behaviour and important edge cases, particularly:

- Event creation and update
- Ticket purchasing
- Insufficient ticket capacity
- Prevention of overselling
- Capacity changes after tickets have been sold
- Soft deletion
- Availability
- Sales reporting
- Concurrent ticket purchase and event update/delete behaviour

Database behaviour will primarily be exercised through integration tests.

## Running the Project

Setup and execution instructions will be added as the application implementation is completed.

The SQL Server database scripts are located in the `Database` directory.

## Design Scope

This project is intentionally scoped as a small take-home exercise rather than a production ticketing platform.

Areas that would require further consideration in a production system include:

- Authentication and authorization
- Payment processing
- Purchase cancellation and refunds
- Reservation/temporary ticket holds
- Idempotency of purchase requests
- Observability and operational monitoring
- Rate limiting
- Deployment and database migration strategy
- Data-retention policy
- Large-scale/high-contention inventory strategies

## AI Assistance

AI tools were used during development as a review and development aid, including discussion of design trade-offs, concurrency behaviour, code review, and documentation.

The implementation decisions and resulting code were reviewed and understood by the author.