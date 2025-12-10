using Carter;
using MediatR;

namespace AegisDrive.Api.Features.Monitoring.EndPionts;



public class MonitoringEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/v1/monitor")
                       .WithTags("Real-Time Monitor");

        // 1. GET ALL (Paginated Live Map)
        // URL: /api/v1/monitor/live?page=1&pageSize=50&companyId=1
        group.MapGet("/live", async ([AsParameters] GetLiveFleet.Query query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return Results.Ok(result.Value);
        })
        .WithSummary("Get live fleet map data (Redis + SQL Hybrid)")
        .WithDescription("Uses Redis MGET for performance. Filters: ?companyId=1&status=Active");

        // 2. GET SINGLE (Vehicle Detail Popup)
        // URL: /api/v1/monitor/live/{vehicleId}
        group.MapGet("/live/{vehicleId}", async (int vehicleId, ISender sender) =>
        {
            var result = await sender.Send(new GetVehicleLiveState.Query(vehicleId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        })
        .WithSummary("Get live telemetry for a specific vehicle");
    }
}