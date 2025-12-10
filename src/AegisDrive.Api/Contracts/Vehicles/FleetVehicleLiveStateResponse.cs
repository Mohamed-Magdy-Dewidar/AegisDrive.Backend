namespace AegisDrive.Api.Contracts.Vehicles;


public record FleetVehicleLiveStateResponse(
    int VehicleId,
    string PlateNumber,
    string Status,
    FleetLiveLocationResponse? LiveLocation // Nullable if no data yet
);

public record FleetLiveLocationResponse(
       double Latitude,
       double Longitude,
       double SpeedKmh,
       DateTime LastUpdateUtc
   );

