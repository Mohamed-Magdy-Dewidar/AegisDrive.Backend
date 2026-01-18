using AegisDrive.Api.Contracts.Trips;
using AegisDrive.Api.Features.Trips;
using AegisDrive.Api.Shared.Auth;
using AegisDrive.Api.Shared.ResultEndpoint;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AegisDrive.Api.Endpoints;

public class TripEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/fleet/trips")
            .WithTags("Trip Management")
            .RequireAuthorization();





        // POST /api/v1/fleet/trips/start
        group.MapPost("/start", async ([FromBody] StartTripRequest request, ClaimsPrincipal user, ISender sender) =>
        {
            var role = user.FindFirst(ClaimTypes.Role)?.Value;

            // 1. Block Managers
            if (role == AuthConstants.Roles.Manager)
                return Results.BadRequest("Managers cannot start a trip.");

            // 2. Extract and Validate DriverId
            var driverIdClaim = user.FindFirst(AuthConstants.Claims.DriverId)?.Value;
            if (!int.TryParse(driverIdClaim, out int driverId))
            {
                return Results.BadRequest("Valid driver profile not found in token.");
            }

            // 3. Orchestrate the trip start
            var command = new StartTrip.Command(
                request.VehicleId,
                request.DestinationText,
                request.DestinationLat,
                request.DestinationLng,
                driverId
            );

            var result = await sender.Send(command);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(result.Error);
        })
         .WithSummary("Start a new driving trip session");



        // POST /api/v1/fleet/trips/end/{tripId}
        group.MapPost("/end/{tripId:guid}", async (
            Guid tripId,
            ISender sender) =>
        {
            var result = await sender.Send(new EndTrip.Command(tripId));

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(result.Error);
        })
        .WithSummary("End an active trip and calculate safety feedback");
    }

    
}