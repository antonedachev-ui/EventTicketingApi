using EventTicketing.Api.Data.StoredProcedureResults;
using EventTicketing.Models.Responses;
using EventTicketing.Models.Requests;
using Microsoft.Data.SqlClient;
using System.Data;

namespace EventTicketing.Api.Data
{
    // Adapts HTTP DTOs to the stored-procedure contract and maps SQL results back to DTOs.
    // Business outcomes remain enum values; unexpected database failures propagate.
    public sealed class EventDataAccess : IEventDataAccess
    {
        private readonly ISqlStoredProcedureExecutor _executor;

        public EventDataAccess(ISqlStoredProcedureExecutor executor)
        {
            _executor = executor;
        }

        public async Task<OperationResult<EventCreateResult, EventResponse>> CreateEventAsync(
            CreateEventRequest request, CancellationToken cancellationToken = default)
        {
            // New tiers have no ID; SQL assigns their identities when it inserts the TVP rows.
            var tiers = CreatePricingTierTable();
            foreach (var tier in request.PricingTiers)
            {
                tiers.Rows.Add(DBNull.Value, tier.Name, tier.Price, tier.TotalCapacity);
            }

            var result = await _executor.ExecuteAsync<EventResponse?>(
                "dbo.Event_Create",
                parameters => AddEventParameters(parameters, request.Name, request.Description,
                    request.Venue, request.EventDateTimeUtc, tiers),
                (reader, ct) => ReadEventWithTiersAsync(reader, ct, "Event_Create"),
                cancellationToken);

            return new OperationResult<EventCreateResult, EventResponse>
            {
                // The executor returns an integer; the operation exposes only defined outcomes.
                Status = GetStatus<EventCreateResult>(result.ReturnCode, "Event_Create"),
                Data = result.Data
            };
        }

        public async Task<OperationResult<EventGetResult, EventResponse>> GetEventAsync(int eventId, CancellationToken cancellationToken = default)
        {
            var result = await _executor.ExecuteAsync("dbo.Event_Get",
                parameters =>
                {
                    parameters.Add(
                        "@EventId",
                        SqlDbType.Int).Value = eventId;
                },
                async (reader, ct) =>
                {
                    // Event_Get always emits an event result set followed by a tier result set,
                    // even when the event was not found and both sets contain no rows.
                    EventResponse? eventResponse = null;

                    if (await reader.ReadAsync(ct))
                    {
                        eventResponse = MapEvent(reader);
                    }

                    if (!await reader.NextResultAsync(ct))
                    {
                        throw new InvalidOperationException(
                            "Event_Get did not return the expected pricing tier result set.");
                    }

                    var tiers = await reader.ReadListAsync(
                        MapPricingTier,
                        ct);

                    if (eventResponse is not null)
                    {
                        eventResponse.PricingTiers = tiers;
                    }

                    return eventResponse;
                },
                cancellationToken);

            var status = (EventGetResult)result.ReturnCode;

            if (!Enum.IsDefined(status))
            {
                throw new InvalidOperationException(
                    $"Unexpected Event_Get return code: {result.ReturnCode}");
            }

            return new OperationResult<EventGetResult, EventResponse>
            {
                Status = status,
                Data = result.Data
            };
        }

        public async Task<OperationResult<EventGetAllResult, List<EventListItemResponse>>> GetAllEventsAsync(
            CancellationToken cancellationToken = default)
        {
            var result = await _executor.ExecuteAsync(
                "dbo.Event_GetAll",
                null,
                (reader, ct) => reader.ReadListAsync(MapEventListItem, ct),
                cancellationToken);

            return new OperationResult<EventGetAllResult, List<EventListItemResponse>>
            {
                Status = GetStatus<EventGetAllResult>(result.ReturnCode, "Event_GetAll"),
                Data = result.Data
            };
        }

        public async Task<OperationResult<EventUpdateResult, EventResponse>> UpdateEventAsync(
            int eventId, UpdateEventRequest request, CancellationToken cancellationToken = default)
        {
            // The TVP is the desired active tier set: null IDs create tiers; existing IDs
            // update tiers. SQL preserves sold quantity and soft-deletes omitted tiers.
            var tiers = CreatePricingTierTable();
            foreach (var tier in request.PricingTiers)
            {
                tiers.Rows.Add((object?)tier.PricingTierId ?? DBNull.Value,
                    tier.Name, tier.Price, tier.TotalCapacity);
            }

            var result = await _executor.ExecuteAsync<EventResponse?>(
                "dbo.Event_Update",
                parameters =>
                {
                    parameters.Add("@EventId", SqlDbType.Int).Value = eventId;
                    AddEventParameters(parameters, request.Name, request.Description,
                        request.Venue, request.EventDateTimeUtc, tiers);
                },
                (reader, ct) => ReadEventWithTiersAsync(reader, ct, "Event_Update"),
                cancellationToken);

            return new OperationResult<EventUpdateResult, EventResponse>
            {
                Status = GetStatus<EventUpdateResult>(result.ReturnCode, "Event_Update"),
                Data = result.Data
            };
        }

        public async Task<EventDeleteResult> DeleteEventAsync(
            int eventId, CancellationToken cancellationToken = default)
        {
            // Event_Delete has no result set; its RETURN code alone describes the outcome.
            var result = await _executor.ExecuteAsync<object?>(
                "dbo.Event_Delete",
                parameters => parameters.Add("@EventId", SqlDbType.Int).Value = eventId,
                (_, _) => Task.FromResult<object?>(null),
                cancellationToken);

            return GetStatus<EventDeleteResult>(result.ReturnCode, "Event_Delete");
        }

        public async Task<OperationResult<EventAvailabilityResult, EventAvailabilityResponse>> GetEventAvailabilityAsync(
            int eventId, CancellationToken cancellationToken = default)
        {
            // An empty list is valid for an unknown, deleted, or unavailable event.
            var result = await _executor.ExecuteAsync(
                "dbo.Event_GetAvailability",
                parameters => parameters.Add("@EventId", SqlDbType.Int).Value = eventId,
                async (reader, ct) => new EventAvailabilityResponse
                {
                    EventId = eventId,
                    PricingTiers = await reader.ReadListAsync(MapPricingTier, ct)
                },
                cancellationToken);

            return new OperationResult<EventAvailabilityResult, EventAvailabilityResponse>
            {
                Status = GetStatus<EventAvailabilityResult>(result.ReturnCode, "Event_GetAvailability"),
                Data = result.Data
            };
        }

        public async Task<OperationResult<EventSalesSummaryResult, EventSalesSummaryResponse>> GetEventSalesSummaryAsync(
            int eventId, CancellationToken cancellationToken = default)
        {
            // SQL includes soft-deleted tiers and uses the purchase-time UnitPrice.
            var result = await _executor.ExecuteAsync<EventSalesSummaryResponse?>(
                "dbo.Event_GetSalesSummary",
                parameters => parameters.Add("@EventId", SqlDbType.Int).Value = eventId,
                async (reader, ct) => await reader.ReadAsync(ct) ? MapSalesSummary(reader) : null,
                cancellationToken);

            return new OperationResult<EventSalesSummaryResult, EventSalesSummaryResponse>
            {
                Status = GetStatus<EventSalesSummaryResult>(result.ReturnCode, "Event_GetSalesSummary"),
                Data = result.Data
            };
        }

        public async Task<OperationResult<TicketPurchaseResult, TicketPurchaseResponse>> PurchaseTicketAsync(
            PurchaseTicketRequest request, CancellationToken cancellationToken = default)
        {
            // Inventory is decremented conditionally inside Ticket_Purchase's transaction;
            // this call does not treat an earlier availability query as a reservation.
            var result = await _executor.ExecuteAsync<TicketPurchaseResponse?>(
                "dbo.Ticket_Purchase",
                parameters =>
                {
                    parameters.Add("@PricingTierId", SqlDbType.Int).Value = request.PricingTierId;
                    parameters.Add("@Quantity", SqlDbType.Int).Value = request.Quantity;
                },
                async (reader, ct) => reader.FieldCount > 0 && await reader.ReadAsync(ct)
                    ? MapTicketPurchase(reader)
                    : null,
                cancellationToken);

            return new OperationResult<TicketPurchaseResult, TicketPurchaseResponse>
            {
                Status = GetStatus<TicketPurchaseResult>(result.ReturnCode, "Ticket_Purchase"),
                Data = result.Data
            };
        }

        private static void AddEventParameters(SqlParameterCollection parameters,
            string name, string? description, string venue, DateTime eventDateTimeUtc,
            DataTable pricingTiers)
        {
            parameters.Add("@Name", SqlDbType.NVarChar, 256).Value = name;
            parameters.Add("@Description", SqlDbType.NVarChar, 512).Value =
                (object?)description ?? DBNull.Value;
            parameters.Add("@Venue", SqlDbType.NVarChar, 256).Value = venue;
            parameters.Add("@EventDateTimeUtc", SqlDbType.DateTime2).Value = eventDateTimeUtc;
            parameters.Add(new SqlParameter("@PricingTiers", SqlDbType.Structured)
            {
                TypeName = "dbo.PricingTierInputType",
                Value = pricingTiers
            });
        }

        private static DataTable CreatePricingTierTable()
        {
            // SQL Server TVPs match by column position, so order and types must match dbo.PricingTierInputType.
            var tiers = new DataTable();
            tiers.Columns.Add("PricingTierId", typeof(int));
            tiers.Columns.Add("Name", typeof(string));
            tiers.Columns.Add("Price", typeof(decimal));
            tiers.Columns.Add("TotalCapacity", typeof(int));
            return tiers;
        }

        private static async Task<EventResponse?> ReadEventWithTiersAsync(
            SqlDataReader reader, CancellationToken cancellationToken, string procedureName)
        {
            // Create and update return no result sets for expected business failures;
            // the executor still retrieves their RETURN code after this reader closes.
            if (reader.FieldCount == 0)
            {
                return null;
            }

            EventResponse? eventResponse = null;
            if (await reader.ReadAsync(cancellationToken))
            {
                eventResponse = MapEvent(reader);
            }

            if (!await reader.NextResultAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    $"{procedureName} did not return the expected pricing tier result set.");
            }

            var tiers = await reader.ReadListAsync(MapPricingTier, cancellationToken);
            if (eventResponse is not null)
            {
                eventResponse.PricingTiers = tiers;
            }

            return eventResponse;
        }

        private static TStatus GetStatus<TStatus>(int returnCode, string procedureName)
            where TStatus : struct, Enum
        {
            // An unknown code is a broken SQL/C# contract, not a business outcome.
            var status = (TStatus)Enum.ToObject(typeof(TStatus), returnCode);
            if (!Enum.IsDefined(status))
            {
                throw new InvalidOperationException(
                    $"Unexpected {procedureName} return code: {returnCode}");
            }

            return status;
        }

        private static EventResponse MapEvent(SqlDataReader reader)
        {
            var descriptionOrdinal = reader.GetOrdinal("Description");

            return new EventResponse
            {
                EventId = reader.GetInt32(reader.GetOrdinal("EventId")),

                Name = reader.GetString(reader.GetOrdinal("Name")),

                Description = reader.IsDBNull(descriptionOrdinal)
                        ? null
                        : reader.GetString(descriptionOrdinal),

                Venue = reader.GetString(reader.GetOrdinal("Venue")),

                // SQL datetime2 does not retain DateTime.Kind; this column stores UTC values.
                EventDateTimeUtc = DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("EventDateTimeUtc")),DateTimeKind.Utc)
            };
        }

        private static EventListItemResponse MapEventListItem(SqlDataReader reader)
        {
            var descriptionOrdinal = reader.GetOrdinal("Description");

            return new EventListItemResponse
            {
                EventId = reader.GetInt32(reader.GetOrdinal("EventId")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Description = reader.IsDBNull(descriptionOrdinal)
                    ? null
                    : reader.GetString(descriptionOrdinal),
                Venue = reader.GetString(reader.GetOrdinal("Venue")),
                // SQL datetime2 does not retain DateTime.Kind; this column stores UTC values.
                EventDateTimeUtc = DateTime.SpecifyKind(
                    reader.GetDateTime(reader.GetOrdinal("EventDateTimeUtc")), DateTimeKind.Utc)
            };
        }

        private static PricingTierResponse MapPricingTier(SqlDataReader reader)
        {
            return new PricingTierResponse
            {
                PricingTierId = reader.GetInt32(reader.GetOrdinal("PricingTierId")),

                Name = reader.GetString(reader.GetOrdinal("Name")),

                Price = reader.GetDecimal(reader.GetOrdinal("Price")),

                TotalCapacity = reader.GetInt32(reader.GetOrdinal("TotalCapacity")),

                AvailableCapacity = reader.GetInt32(reader.GetOrdinal("AvailableCapacity"))
            };
        }

        private static EventSalesSummaryResponse MapSalesSummary(SqlDataReader reader)
        {
            return new EventSalesSummaryResponse
            {
                EventId = reader.GetInt32(reader.GetOrdinal("EventId")),
                EventName = reader.GetString(reader.GetOrdinal("EventName")),
                TicketsSold = reader.GetInt32(reader.GetOrdinal("TicketsSold")),
                TotalRevenue = reader.GetDecimal(reader.GetOrdinal("TotalRevenue"))
            };
        }

        private static TicketPurchaseResponse MapTicketPurchase(SqlDataReader reader)
        {
            return new TicketPurchaseResponse
            {
                PurchaseId = reader.GetInt64(reader.GetOrdinal("PurchaseId")),
                PricingTierId = reader.GetInt32(reader.GetOrdinal("PricingTierId")),
                Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                UnitPrice = reader.GetDecimal(reader.GetOrdinal("UnitPrice")),
                // SQL datetime2 does not retain DateTime.Kind; this column stores UTC values.
                PurchasedAtUtc = DateTime.SpecifyKind(
                    reader.GetDateTime(reader.GetOrdinal("PurchasedAtUtc")), DateTimeKind.Utc)
            };
        }
    }
}
