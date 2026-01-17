using AegisDrive.Api.Features.Analytics;
using AegisDrive.Api.Shared.Auth;
using Carter;
using MediatR;
using System.Security.Claims;

namespace AegisDrive.Api.Features.Monitoring.EndPionts;



public class MonitoringEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/v1/monitor")
                       .WithTags("Real-Time Monitor");

        // 1. GET ALL (Paginated Live Map)
        // URL: /api/v1/monitor/live?page=1&pageSize=50&companyId=1
        group.MapGet("/live", async ([AsParameters] GetLiveFleet.Query query,ClaimsPrincipal user, ISender sender) =>
        {
            var role = user.FindFirst(ClaimTypes.Role)?.Value;

            if (role == AuthConstants.Roles.Individual)
            {
                var driverIdClaim = user.FindFirst(AuthConstants.Claims.DriverId)?.Value;

                if (int.TryParse(driverIdClaim, out int driverId))
                {
                    query = query with { DriverId = driverId, CompanyId = null };
                }
                else
                {
                    return Results.BadRequest("Driver profile not found.");
                }
            }
            else if (role == AuthConstants.Roles.Manager)
            {
                var companyIdClaim = user.FindFirst(AuthConstants.Claims.CompanyId)?.Value;

                if (int.TryParse(companyIdClaim, out int compId))
                {
                    query = query with { CompanyId = compId };
                }
            }
            // If Admin, we leave the query as-is (allows ?companyId=5 in URL)

            var result = await sender.Send(query);

            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        })
        .RequireAuthorization()
        .WithSummary("Get live fleet map data (Redis + SQL Hybrid)")
        .WithDescription("Uses Redis MGET for performance. Filters: ?companyId=1&status=Active");

        // 2. GET SINGLE (Vehicle Detail Popup)
        // URL: /api/v1/monitor/live/{vehicleId}
        group.MapGet("/live/{vehicleId}", async (int vehicleId, ClaimsPrincipal user, ISender sender) =>
        {           
            var result = await sender.Send(new GetVehicleLiveState.Query(vehicleId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        })
        .RequireAuthorization()
        .WithSummary("Get live telemetry for a specific vehicle");
    }
}