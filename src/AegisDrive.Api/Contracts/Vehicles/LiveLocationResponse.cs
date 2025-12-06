namespace AegisDrive.Api.Contracts.Vehicles;

public record LiveLocationResponse(
       double Latitude,
       double Longitude,
       double SpeedKmh,
       DateTime LastUpdateUtc
   );

public record VehicleLiveStateResponse(
    int VehicleId,
    string PlateNumber,
    string Status, // "Active", "Maintenance"
    LiveLocationResponse? LiveLocation // Nullable if no data yet
);