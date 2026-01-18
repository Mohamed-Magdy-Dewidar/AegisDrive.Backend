using AegisDrive.Api.Entities.Enums;

namespace AegisDrive.Api.Entities;

public class Trip : BaseEntity<Guid>
{
    public int DriverId { get; set; }
    public int VehicleId { get; set; }
    public TripStatus Status { get; set; }

    // Start Location
    public decimal StartLat { get; set; }
    public decimal StartLng { get; set; }

    public decimal EndLat { get; set; } = 0.0M;
    public decimal EndLng { get; set; } = 0.0M;
    
    public DateTime StartTime { get; set; }

    // Destination Data (Provided by Frontend)
    public string DestinationText { get; set; } = string.Empty;
    public decimal DestinationLat { get; set; }
    public decimal DestinationLng { get; set; }

    // OSRM Calculated Data
    public double EstimatedDistanceMeters { get; set; }
    public double EstimatedDurationSeconds { get; set; }
    public string? RouteGeometryJson { get; set; } // The blue line coordinates

    // Final Summary
    public DateTime? EndTime { get; set; }
    public double TripSafetyScore { get; set; } = 100;
}