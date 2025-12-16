using AegisDrive.Api.Shared.Auth;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AegisDrive.Api.Features.SafetyEvents;

public class SafetyEventEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/v1");

        // 1. INGESTION (HTTP Fallback)
        group.MapPost("/ingest/safety-event", async ([FromBody] CreateSafetyEvent.Command command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Accepted(value: result.Value) : Results.BadRequest(result.Error);
        })
        .WithTags("Ingestion")
        .WithSummary("Ingest a safety event (HTTP fallback for SQS)");




        group.MapGet("/safety-events", async ([AsParameters] GetSafetyEvents.Query query, ISender sender, ClaimsPrincipal user) =>
        {
            var role = user.FindFirst(ClaimTypes.Role)?.Value;
            var companyIdClaim = user.FindFirst(AuthConstants.Claims.CompanyId)?.Value;

            // get the real comapny_id from the token 
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
                    // Edge case: User is logged in but has no Driver profile created yet
                    return Results.BadRequest("Driver profile not found for this user.");
                }
            }            

            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        })
         .RequireAuthorization()
         .WithTags("Analytics")
         .WithSummary("Get a filterable list of safety events (Evidence Feed)");


        group.MapGet("/safety-events-without-auth", async ([AsParameters] GetSafetyEvents.Query query, ISender sender, ClaimsPrincipal user) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        })
        .WithTags("Analytics")
        .WithSummary("Get a filterable list of safety events (Evidence Feed) without auth");






        // 2. ANALYTICS (Get Details)
        group.MapGet("/incidents/{id}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetSafetyEventDetails.Query(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        })
        .WithTags("Analytics")
        .WithSummary("Get full details of a specific safety event");
    }
}