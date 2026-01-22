using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Entities.Enums.Driver;
using System.Text.Json.Serialization;

namespace AegisDrive.Api.Entities;

public class SafetyEvent : BaseEntity<Guid>
{  


    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AlertLevel AlertLevel { get; set; } = AlertLevel.NONE;


    [JsonConverter(typeof(JsonStringEnumConverter))]

    public DriverState DriverState { get; set; } = DriverState.ALERT;


    public string? Message { get; set; }

    public double? EarValue { get; set; }
    public double? MarValue { get; set; }
    public double? HeadYaw { get; set; }

    public string? S3DriverImagePath { get; set; }
    public string? S3RoadImagePath { get; set; }

    // Flattened Road Context
    public bool RoadHasHazard { get; set; }
    public int RoadVehicleCount { get; set; }
    public int RoadPedestrianCount { get; set; }
    public double? RoadClosestDistance { get; set; }
    
    public DateTime Timestamp { get; set; }


    // Telemetry
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public double Speed { get; set; }

    public string? DeviceId { get; set; }
    public int? VehicleId { get; set; }
    public int? DriverId { get; set; }

    public Guid? TripId { get; set; }

    public Device? Device { get; set; }
    public Vehicle? Vehicle { get; set; }
    public Driver? Driver { get; set; }

    public Trip? Trip { get; set; }


}
