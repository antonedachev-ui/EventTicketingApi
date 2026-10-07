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

        public static ActionResult<List<EventListItemResponse>> ToActionResult(
            this OperationResult<EventGetAllResult, List<EventListItemResponse>> result,
            ControllerBase controller)
        {
            return result.Status switch
            {
                EventGetAllResult.Success when result.Data is not null => controller.Ok(result.Data),
                EventGetAllResult.Success => throw new InvalidOperationException(
                    "Event_GetAll succeeded without returning an event list."),
                EventGetAllResult.EventNotFound => controller.NotFound(),
                _ => throw new InvalidOperationException($"Unexpected Event_GetAll result: {result.Status}")
            };
        }

        public static ActionResult<EventResponse> ToActionResult(
            this OperationResult<EventCreateResult, EventResponse> result,
            ControllerBase controller)
        {
            return result.Status switch
            {
                EventCreateResult.Success when result.Data is not null =>
                    controller.CreatedAtAction(nameof(EventsController.GetEvent),
                        new { eventId = result.Data.EventId }, result.Data),
                EventCreateResult.Success => throw new InvalidOperationException(
                    "Event_Create succeeded without returning an event."),
                EventCreateResult.EventNameInvalid => BadRequest(controller, "Event name is required."),
                EventCreateResult.VenueNameInvalid => BadRequest(controller, "Venue is required."),
                EventCreateResult.PricingTierMissing => BadRequest(controller, "At least one pricing tier is required."),
                EventCreateResult.PricingTierNameInvalid => BadRequest(controller, "Pricing tier name is required."),
                EventCreateResult.DuplicatePricingTierNames => BadRequest(controller, "Pricing tier names must be unique."),
                EventCreateResult.PricingTierInvalidPrice => BadRequest(controller, "Pricing tier price cannot be negative."),
                EventCreateResult.PricingTierInvalidTotalCapacity => BadRequest(controller, "Pricing tier capacity must be positive."),
                EventCreateResult.InvalidPricingTierId => BadRequest(controller, "New pricing tiers cannot have an ID."),
                _ => throw new InvalidOperationException($"Unexpected Event_Create result: {result.Status}")
            };
        }

        public static ActionResult<EventResponse> ToActionResult(
            this OperationResult<EventUpdateResult, EventResponse> result,
            ControllerBase controller)
        {
            return result.Status switch
            {
                EventUpdateResult.Success => controller.Ok(result.Data),
                EventUpdateResult.EventNotFound => controller.NotFound(),
                EventUpdateResult.EventNameInvalid => BadRequest(controller, "Event name is required."),
                EventUpdateResult.VenueNameInvalid => BadRequest(controller, "Venue is required."),
                EventUpdateResult.PricingTierMissing => BadRequest(controller, "At least one pricing tier is required."),
                EventUpdateResult.PricingTierNameInvalid => BadRequest(controller, "Pricing tier name is required."),
                EventUpdateResult.DuplicatePricingTierNames => BadRequest(controller, "Pricing tier names must be unique."),
                EventUpdateResult.PricingTierInvalidPrice => BadRequest(controller, "Pricing tier price cannot be negative."),
                EventUpdateResult.PricingTierInvalidTotalCapacity => Conflict(controller,
                    "Pricing tier capacity must be positive and cannot be less than tickets already sold."),
                EventUpdateResult.InvalidPricingTierId => BadRequest(controller, "A pricing tier ID is invalid for this event."),
                _ => throw new InvalidOperationException($"Unexpected Event_Update result: {result.Status}")
            };
        }

        public static IActionResult ToActionResult(this EventDeleteResult result, ControllerBase controller)
        {
            return result switch
            {
                EventDeleteResult.Success => controller.NoContent(),
                EventDeleteResult.EventNotFound => controller.NotFound(),
                _ => throw new InvalidOperationException($"Unexpected Event_Delete result: {result}")
            };
        }

        public static ActionResult<EventAvailabilityResponse> ToActionResult(
            this OperationResult<EventAvailabilityResult, EventAvailabilityResponse> result,
            ControllerBase controller)
        {
            return result.Status switch
            {
                EventAvailabilityResult.Success => controller.Ok(result.Data),
                _ => throw new InvalidOperationException($"Unexpected Event_GetAvailability result: {result.Status}")
            };
        }

        public static ActionResult<EventSalesSummaryResponse> ToActionResult(
            this OperationResult<EventSalesSummaryResult, EventSalesSummaryResponse> result,
            ControllerBase controller)
        {
            return result.Status switch
            {
                EventSalesSummaryResult.Success when result.Data is null => controller.NotFound(),
                EventSalesSummaryResult.Success => controller.Ok(result.Data),
                _ => throw new InvalidOperationException($"Unexpected Event_GetSalesSummary result: {result.Status}")
            };
        }

        public static ActionResult<TicketPurchaseResponse> ToActionResult(
            this OperationResult<TicketPurchaseResult, TicketPurchaseResponse> result,
            ControllerBase controller)
        {
            return result.Status switch
            {
                TicketPurchaseResult.Success when result.Data is not null =>
                    controller.StatusCode(StatusCodes.Status201Created, result.Data),
                TicketPurchaseResult.Success => throw new InvalidOperationException(
                    "Ticket_Purchase succeeded without returning a purchase."),
                TicketPurchaseResult.PricingTierNotAvailable => controller.NotFound(),
                TicketPurchaseResult.InsufficientCapacity => Conflict(controller, "Insufficient ticket capacity."),
                TicketPurchaseResult.InvalidQuantity => BadRequest(controller, "Quantity must be positive."),
                _ => throw new InvalidOperationException($"Unexpected Ticket_Purchase result: {result.Status}")
            };
        }

        private static BadRequestObjectResult BadRequest(ControllerBase controller, string detail) =>
            controller.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid request",
                Detail = detail
            });

        private static ConflictObjectResult Conflict(ControllerBase controller, string detail) =>
            controller.Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Request conflicts with current state",
                Detail = detail
            });

    }
}
