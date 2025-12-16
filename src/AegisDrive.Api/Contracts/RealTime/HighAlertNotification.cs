namespace AegisDrive.Api.Contracts.RealTime;

public record HighAlertNotification(
    Guid EventId,
    string PlateNumber,
    string DriverState,     // e.g., "Microsleep"
    string AlertLevel,      // e.g., "Critical"
    string Message,
    string MapLink,
    double SpeedKmh,
    DateTime Timestamp,
    string? DriverImageUrl
);