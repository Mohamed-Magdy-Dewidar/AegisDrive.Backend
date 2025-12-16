using AegisDrive.Api.Shared.Auth;
using Carter;
using MediatR;
using System.Security.Claims;

namespace AegisDrive.Api.Features.Analytics;

public class AnalyticsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/v1/analytics").RequireAuthorization();

        group.MapGet("/dashboard", async (ISender sender, ClaimsPrincipal user) =>
        {
            var role = user.FindFirst(ClaimTypes.Role)?.Value;
            var query = new GetDashboardStats.Query(null, null);

            // Context Resolution
            if (role == AuthConstants.Roles.Individual)
            {
                var driverIdClaim = user.FindFirst(AuthConstants.Claims.DriverId)?.Value;
                if (int.TryParse(driverIdClaim, out int driverId))
                {
                    query = query with { DriverId = driverId };
                }
                else return Results.BadRequest("Driver profile not found.");
            }
            else if (role == AuthConstants.Roles.Manager)
            {
                var companyIdClaim = user.FindFirst(AuthConstants.Claims.CompanyId)?.Value;
                if (int.TryParse(companyIdClaim, out int compId))
                {
                    query = query with { CompanyId = compId };
                }
            }

            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        })
        .WithTags("Analytics")
        .WithSummary("Get high-level dashboard statistics (Score, Alerts, Counts)")
        .RequireAuthorization();
    }
}