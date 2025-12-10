namespace AegisDrive.Api.Contracts.Telemetry;


public record IngestTelemetryRequest(
        string DeviceId,
        double Latitude,
        double Longitude,
        double SpeedKmh,
        string EventType,
        DateTime Timestamp
    );