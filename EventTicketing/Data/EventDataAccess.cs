using EventTicketing.Api.Data.StoredProcedureResults;
using EventTicketing.Models.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace EventTicketing.Api.Data
{
    public sealed class EventDataAccess : IEventDataAccess
    {
        private readonly ISqlStoredProcedureExecutor _executor;

        public EventDataAccess(ISqlStoredProcedureExecutor executor)
        {
            _executor = executor;
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
    }
}
