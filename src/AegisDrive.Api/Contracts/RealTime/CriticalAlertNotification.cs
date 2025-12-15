namespace AegisDrive.Api.Contracts.RealTime;

public record CriticalAlertNotification(
    int VehicleId,
    string PlateNumber,
    string AlertType, // "Drowsiness", "Distraction", "Crash"
    string Severity,
    DateTime Timestamp
);
