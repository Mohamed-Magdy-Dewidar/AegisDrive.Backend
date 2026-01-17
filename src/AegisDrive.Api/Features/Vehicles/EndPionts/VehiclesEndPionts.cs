using AegisDrive.Api.Contracts.Vehicles;
using AegisDrive.Api.Shared.Auth;
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

        // GET /api/v1/fleet/vehicles/id
        group.MapGet("/vehicles/{id}", async (int id, ISender sender) =>
        {            
            var query = new GetVehicle.Query(id);
            var result = await sender.Send(query);
            if (result.IsSuccess)
            {
                return Results.Ok(result.Value); // Return the vehicle details if successful
            }

            return Results.NotFound(result.Error.Message); // Return error message if not found
        })
            .WithSummary("Get vehicle details by ID")
            .WithDescription("Retrieves the details of a vehicle based on its ID, using caching for better performance.");


        // GET /api/v1/fleet/vehicles
        group.MapGet("/vehicles", async ([AsParameters] ListVehicles.Query query, ISender sender , ClaimsPrincipal user) =>
        {
            var role = user.FindFirst(ClaimTypes.Role)?.Value;
            var companyIdClaim = user.FindFirst(AuthConstants.Claims.CompanyId)?.Value;

            // Get the real company_id from the token 
            if (int.TryParse(companyIdClaim, out int company_id))
            {
                query = query with { CompanyId = company_id };
            }
            else
            {
                query = query with { CompanyId = null };
            }

            if (role == AuthConstants.AccountTypes.Individual)
            {
                var driverIdClaim = user.FindFirst(AuthConstants.Claims.DriverId)?.Value;
                if (int.TryParse(driverIdClaim, out int driverId))
                {
                    query = query with { DriverId = driverId };
                }
                else
                {
                    query = query with { DriverId = null }; 
                }
            }

            var result = await sender.Send(query);
            return Results.Ok(result.Value);
        })
        .WithSummary("List vehicles with filtering and pagination")
        .WithDescription("Filters: ?companyId=1&status=Active&page=1&pageSize=20")
        .RequireAuthorization();



        // GET /api/v1/fleet/vehicles/unassigned
        group.MapGet("/vehicles/unassigned", async (
            [AsParameters] GetAllUnAssignedVehciles.Query query,
            ISender sender,
            ClaimsPrincipal user) =>
        {
            var companyIdClaim = user.FindFirst(AuthConstants.Claims.CompanyId)?.Value;
            if (int.TryParse(companyIdClaim, out int companyId))
            {
                query = query with { CompanyId = companyId };
            }

            var role = user.FindFirst(ClaimTypes.Role)?.Value;
            if (role == AuthConstants.AccountTypes.Individual)
            {
                var driverIdClaim = user.FindFirst(AuthConstants.Claims.DriverId)?.Value;
                query = query with
                {
                    DriverId = int.TryParse(driverIdClaim, out int driverId) ? driverId : null,
                    CompanyId = null
                };
            }

            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        })
        .WithSummary("Get available vehicles (Unassigned)")
        .RequireAuthorization();

        // GET /api/v1/fleet/vehicles/assigned
        group.MapGet("/vehicles/assigned", async (
            [AsParameters] GetAssignedVehicles.Query query,
            ISender sender,
            ClaimsPrincipal user) =>
        {
            var companyIdClaim = user.FindFirst(AuthConstants.Claims.CompanyId)?.Value;
            if (int.TryParse(companyIdClaim, out int companyId))
            {
                query = query with { CompanyId = companyId };
            }

         
            var role = user.FindFirst(ClaimTypes.Role)?.Value;
            if (role == AuthConstants.AccountTypes.Individual)
            {
                var driverIdClaim = user.FindFirst(AuthConstants.Claims.DriverId)?.Value;
                query = query with
                {
                    DriverId = int.TryParse(driverIdClaim, out int driverId) ? driverId : null,
                    CompanyId = null
                };
            }

            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        })
        .WithSummary("Get currently active vehicles (Assigned)")
        .RequireAuthorization();

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
        group.MapPost("/assignments/end/{vehicleId:int}", async (int vehicleId, ISender sender) =>
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

