using AegisDrive.Api.Contracts.Devices;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AegisDrive.Api.Features.Devices;

public class DeviceEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Create a dedicated group for Device Management
        // This applies the Tag and Auth requirement to all endpoints inside
        var group = app.MapGroup("api/v1/fleet")
                       .WithTags("Device Management")
                       .RequireAuthorization();

        // 1. Link Device
        // POST /api/v1/fleet/vehicles/{vehicleId}/link-device
        group.MapPost("/vehicles/{vehicleId}/link-device", async (int vehicleId, [FromBody] LinkDeviceRequest req, ISender sender) =>
        {
            var command = new LinkDevice.Command(vehicleId, req.DeviceId, req.Type);
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        })
        .WithSummary("Link (or Create) an IoT Device to a Vehicle");

        // 2. Get Device
        // GET /api/v1/fleet/devices/{deviceId}
        group.MapGet("/devices/{deviceId}", async (string deviceId, ISender sender) =>
        {
            // Note: Ensure you reference the correct Query class (GetDeviceById.Query)
            var result = await sender.Send(new GetDeviceById.Query(deviceId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        })
        .WithSummary("Get device details and vehicle context (Cached)");
    }
}