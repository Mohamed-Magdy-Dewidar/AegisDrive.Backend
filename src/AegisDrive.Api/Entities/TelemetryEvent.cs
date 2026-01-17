using AegisDrive.Api.Entities.Enums;

namespace AegisDrive.Api.Entities;

public class TelemetryEvent : BaseEntity<Guid>
{

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double SpeedKmh { get; set; }

    //public double Heading { get; set; }
    //public double GForceX { get; set; }
    //public double GForceY { get; set; }
    //public double GForceZ { get; set; }

    public TelemetryEventType EventType { get; set; } = TelemetryEventType.GpsUpdate;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string? DeviceId { get; set; }
    public int? VehicleId { get; set; }

    public Device? Device { get; set; }
    public Vehicle? Vehicle { get; set; }

}
