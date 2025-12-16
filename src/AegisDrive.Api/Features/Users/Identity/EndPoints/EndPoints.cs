using Carter;
using MediatR;
using System.Security.Claims;

namespace AegisDrive.Api.Features.Users.Identity.EndPoints;

public class EndPoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("api/v1/users/me", async (ClaimsPrincipal user, ISender sender) =>
        {

            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            // Delegate to Handler
            var result = await sender.Send(new GetCurrentUser.Query(userId));

            return result.IsSuccess
                ? Results.Ok(result.Value) // Returns the uniform Result<T> structure
                : Results.BadRequest(result.Error);
        })
        .RequireAuthorization()
        .WithTags("Me")
        .WithSummary("Get current user profile (Cached)");
    }
}