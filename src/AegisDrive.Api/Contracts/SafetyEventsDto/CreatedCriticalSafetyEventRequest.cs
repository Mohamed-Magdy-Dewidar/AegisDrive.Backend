using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Entities.Enums.Driver;

namespace AegisDrive.Api.Contracts.SafetyEventsDto;

public record CreatedCriticalSafetyEventRequest(
        Guid EventId,
        string? Message,
        double? EarValue,
        double? MarValue,
        double? HeadYaw,
        DriverState DriverState,
        AlertLevel AlertLevel,
        string? S3DriverImagePath,
        string? S3RoadImagePath,
        bool RoadHasHazard,
        int RoadVehicleCount,
        int RoadPedestrianCount,
        double? RoadClosestDistance,
        DateTime Timestamp,
        string? DeviceId,
        int? VehicleId,
        int? DriverId,
        int? CompanyId
     );