using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Entities.Enums.Driver;

namespace AegisDrive.Api.Contracts.SafetyEventsDto;

public record GetSafetyEventsResponse(
       Guid Id,
       string Message,
       DriverState DriverState,
       AlertLevel AlertLevel,
       DateTime Timestamp,
       string VehiclePlate,
       string DriverName
   );