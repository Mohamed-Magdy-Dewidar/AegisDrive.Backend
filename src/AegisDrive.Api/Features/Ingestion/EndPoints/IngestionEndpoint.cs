using AegisDrive.Api.Contracts.Telemetry;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AegisDrive.Api.Features.Ingestion;

public class IngestionEndpoint : ICarterModule
{

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("api/v1/ingest/telemetry", async ([FromBody] IngestTelemetryRequest request, ISender sender) =>
        {
            var Command = new IngestTelemetry.Command(request.DeviceId, request.Latitude, request.Longitude, request.SpeedKmh, request.EventType, request.Timestamp);
            var result = await sender.Send(Command);
            return result.IsSuccess ? Results.Accepted() : Results.BadRequest(result.Error);
        })
        .WithTags("Ingestion")
        .WithSummary("Ingest GPS telemetry (Updates Redis & SignalR)");
    }

}