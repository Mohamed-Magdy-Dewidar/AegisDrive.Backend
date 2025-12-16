using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Entities.Enums.Driver;

namespace AegisDrive.Api.Contracts.SafetyEventsDto;

public record GetSafetyEventsResponse(
       Guid Id,
       string Message,
       DriverState DriverState,
       AlertLevel AlertLevel,
       string? DriverImageUrl,
       string? RoadImageUrl,
       DateTime Timestamp,
       string VehiclePlate,
       string DriverName
   );