namespace AegisDrive.Api.Entities.Enums;

public enum TelemetryEventType
{
    GpsUpdate,          // Standard location ping
    HarshBrake,         // Sudden deceleration (G-Force > threshold)
    HarshAcceleration,  // Sudden speed increase
    HarshCornering,     // High lateral G-Force
    Crash,              // Impact detection
    IgnitionOn,         // Engine start
    IgnitionOff         // Engine stop
}