using EventTicketing.Api.Data;
using EventTicketing.Api.Data.StoredProcedureResults;
using EventTicketing.Models.Responses;
using Microsoft.AspNetCore.Mvc;

namespace EventTicketing.Api.Controllers
{
    public static class ResultMappingExtensions
    {
        public static ActionResult<EventResponse> ToActionResult(
        this OperationResult<EventGetResult, EventResponse> result,
        ControllerBase controller)
        {
            return result.Status switch
            {
                EventGetResult.Success => controller.Ok(result.Data),

                EventGetResult.EventNotFound => controller.NotFound(),

                _ => throw new InvalidOperationException($"Unexpected Event_Get result: {result.Status}")
            };
        }

    }
}
