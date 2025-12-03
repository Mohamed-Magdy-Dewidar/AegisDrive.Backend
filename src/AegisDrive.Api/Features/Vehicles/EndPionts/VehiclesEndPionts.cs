using AegisDrive.Api.Contracts.Vehicles;
using AegisDrive.Api.Features.Fleet;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;



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

        // =================================================================
        // ASSIGNMENTS (SHIFTS)
        // =================================================================

        // POST /api/v1/fleet/assignments/start
        group.MapPost("/assignments/start", async ([FromBody] StartShiftRequest request, ISender sender) =>
        {
            var command = new StartShift.Command(request.DriverId, request.VehicleId);
            var result = await sender.Send(command);

            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            return Results.Ok(result.Value);
        })
        .WithSummary("Start a driver shift (Assign driver to vehicle)");


        // POST /api/v1/fleet/assignments/end/{vehicleId}
        group.MapPost("/assignments/end/{vehicleId:int}", async (int vehicleId, ISender sender) =>
        {
            var command = new EndShift.Command(vehicleId);
            var result = await sender.Send(command);

            if (result.IsFailure)
                return Results.BadRequest(result.Error);

            return Results.Ok(result.Value);
        })
        .WithSummary("End a driver shift (Unassign driver from vehicle)");


    }


}

