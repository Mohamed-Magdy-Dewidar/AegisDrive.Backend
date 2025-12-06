using Azure.Core;
using Carter;
using MediatR;

namespace AegisDrive.Api.Features.SafetyEvents;

public class SafetyEventsEndPionts : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {

      //  app.MapPost("api/v1/ingest/safety-event", async ([FromBody] Request request, ISender sender) =>
      //  {
      //      // Map DTO -> Command
      //      var command = new Command(
      //          request.EventId, request.Message, request.EarValue, request.MarValue, request.HeadYaw,
      //          request.DriverState, request.AlertLevel, request.S3DriverImagePath, request.S3RoadImagePath,
      //          request.RoadHasHazard, request.RoadVehicleCount, request.RoadPedestrianCount, request.RoadClosestDistance,
      //          request.Timestamp, request.DeviceId, request.VehicleId, request.DriverId, request.CompanyId
      //      );

      //      var result = await sender.Send(command);

      //      return result.IsSuccess ? Results.Accepted(value: result.Value) : Results.BadRequest(result.Error);
      //  })
      //.WithTags("Ingestion")
      //.WithSummary("Ingest a safety event (HTTP fallback)");
    }
}
