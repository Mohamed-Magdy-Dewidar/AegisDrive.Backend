using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;

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