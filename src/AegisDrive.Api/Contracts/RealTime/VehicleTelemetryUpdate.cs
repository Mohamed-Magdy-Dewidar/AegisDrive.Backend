namespace AegisDrive.Api.Contracts.RealTime;

// 1. The DTOs (The data we send)
public record VehicleTelemetryUpdate(
    int VehicleId,
    string PlateNumber,
    double Latitude,
    double Longitude,
    double SpeedKmh,
    string Status, // "Moving", "Idle", "Stopped"
    DateTime Timestamp
);
