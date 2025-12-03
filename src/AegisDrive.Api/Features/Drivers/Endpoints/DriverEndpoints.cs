using AegisDrive.Api.Contracts.Drivers;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AegisDrive.Api.Features.Drivers;

public class DriverEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {


        // =================================================================
        // 1. FLEET MANAGEMENT (Driver CRUD)
        // Base Path: /api/v1/fleet/drivers
        // =================================================================
        var fleetGroup = app.MapGroup("api/v1/fleet/drivers")
                            .WithTags("Drivers");




        // POST /api/v1/fleet/drivers
        fleetGroup.MapPost("/", async ([FromForm] RegisterDriverRequest request, ISender sender) =>
        {
            int? companyId = string.IsNullOrWhiteSpace(request.CompanyId) ? null : int.Parse(request.CompanyId);

            var command = new RegisterDriver.Command(
                request.FullName,
                request.PhoneNumber,
                request.Email,
                request.Image,
                companyId
            );

            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        })
        .WithSummary("Register a new driver")
        .DisableAntiforgery();


        // PUT /api/v1/fleet/drivers
        fleetGroup.MapPut("/", async ([FromForm] UpdateDriverRequest request, ISender sender) =>
        {
            int? companyId = string.IsNullOrWhiteSpace(request.CompanyId) ? null : int.Parse(request.CompanyId);

            var command = new UpdateDriver.Command(
                request.DriverId,
                request.FullName,
                request.PhoneNumber,
                request.Email,
                companyId
            );

            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        })
        .WithSummary("Update driver profile")
        .DisableAntiforgery();



        // POST /api/v1/fleet/drivers/{driverId}/upload-image
        fleetGroup.MapPost("/{driverId}/upload-image", async (int driverId, IFormFile image, ISender sender) =>
        {
            var command = new DriverImageUpload.Command(driverId, image);
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        })
        .WithSummary("Uplouad Driver Profile Picture")
        .DisableAntiforgery();
        


        // GET /api/v1/fleet/drivers/{id}
        fleetGroup.MapGet("/{id}", async (int id, ISender sender) =>
        {
            var query = new GetDriverProfile.Query(id);
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        })
        .WithSummary("Get driver profile by ID");

        // DELETE /api/v1/fleet/drivers/{id}
        fleetGroup.MapDelete("/{id}", async (int id, ISender sender) =>
        {
            var command = new DeleteDriver.Command(id);
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        })
        .WithSummary("Delete a driver");

        // GET /api/v1/fleet/drivers
        fleetGroup.MapGet("/", async ([AsParameters] ListDrivers.Query query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return Results.Ok(result.Value);
        })
        .WithSummary("List drivers with filtering and pagination");


        // =================================================================
        // 2. EMERGENCY CONTACTS
        // Base Path: /api/v1/drivers/{driverId}/family-members
        // =================================================================
        var contactGroup = app.MapGroup("api/v1/drivers/{driverId}/family-members")
                              .WithTags("Emergency Contacts");

        // POST /api/v1/drivers/{id}/family-members
        contactGroup.MapPost("/", async (int driverId, [FromBody] AddFamilyMemberRequest request, ISender sender) =>
        {
            // Map Request + Route Param -> Command
            var command = new AddDriverFamilyMember.Command(
                request.FullName,
                request.PhoneNumber,
                request.Email,
                request.Relationship,
                request.NotifyOnCritical,
                driverId // Inject ID from Route
            );

            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        })
        .WithSummary("Add an emergency contact");

        // GET /api/v1/drivers/{id}/family-members
        contactGroup.MapGet("/", async (int driverId, ISender sender) =>
        {
            var query = new ListDriverFamilyMembers.Query(driverId);
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        })
        .WithSummary("List emergency contacts");
    }
}