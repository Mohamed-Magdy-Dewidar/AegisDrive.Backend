using AegisDrive.Api.Contracts.Vehicles;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;



namespace AegisDrive.Api.Features.Vehicles;



public class VehiclesEndPionts : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/v1/fleet")
                      .WithTags("Fleet Management");



        // =================================================================
        // VEHICLES
        // =================================================================

        // GET /api/v1/fleet/vehicles
        group.MapGet("/vehicles", async ([AsParameters] ListVehicles.Query query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return Results.Ok(result.Value);
        })
        .WithSummary("List vehicles with filtering and pagination")
        .WithDescription("Filters: ?companyId=1&status=Active&page=1&pageSize=20");

        // POST /api/v1/fleet/vehicles
        group.MapPost("/vehicles", async ([FromBody] RegisterVehicleRequest request, ISender sender) =>
        {

            int? companyId = string.IsNullOrWhiteSpace(request.CompanyId) ? null : int.Parse(request.CompanyId);

            var command = new RegisterVehicle.Command(
                request.PlateNumber,
                request.Model,
                companyId
            );

            var result = await sender.Send(command);

            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            return Results.Ok(result.Value);
        })
        .WithSummary("Register a new vehicle");


        // Delete /api/v1/fleet/vehicles
        group.MapDelete("/vehicles/{id}", async (int id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteVehicle.Command(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        })
         .RequireAuthorization()
         .WithSummary("Delete a vehicle (Must be unassigned)");



        // PUT /api/v1/fleet/vehicles/{id}
        group.MapPut("/vehicles/{id}", async (int id, [FromBody] UpdateVehicleRequest request, ISender sender) =>
        {
            if (id != request.VehicleId) return Results.BadRequest("ID mismatch");
            var command = new UpdateVehicle.Command(
                request.VehicleId,
                request.PlateNumber,
                request.Model,
                request.Status
            );
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        })
          .RequireAuthorization()
          .WithSummary("Update vehicle details");


       


        // =================================================================
        // ASSIGNMENTS (SHIFTS)
        // =================================================================


        // POST /api/v1/fleet/assignments/start        
        group.MapPost("/assignments/start", async ([FromBody] StartShiftRequest request, ClaimsPrincipal user, ISender sender) =>
        {

            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }
            var command = new StartShift.Command(request.DriverId, request.VehicleId, userId);
            var result = await sender.Send(command);

            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            return Results.Ok(result.Value);
        })
        .RequireAuthorization() 
        .WithSummary("Start a driver shift (Assign driver to vehicle)");




        // =================================================================
        // POST /api/v1/fleet/assignments/end/{vehicleId}
        // =================================================================              
        group.MapPost("/assignments/end{vehicleId:int}", async (int vehicleId, ISender sender) =>
        {
            var command = new EndShift.Command(vehicleId);
            var result = await sender.Send(command);

            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            return Results.Ok(result.Value);
        })
        .RequireAuthorization()
        .WithSummary("End a driver shift (Unassign driver from vehicle)");




    }


}

