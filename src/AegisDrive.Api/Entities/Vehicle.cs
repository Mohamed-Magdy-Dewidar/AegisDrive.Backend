using AegisDrive.Api.Entities.Enums;
using System.Text.Json.Serialization;

namespace AegisDrive.Api.Entities;

public class Vehicle : BaseEntity<int>
{
    public int? CompanyId { get; set; }
    public int? CurrentDriverId { get; set; } // Optimization for fast lookup
    public string? PlateNumber { get; set; } = string.Empty;
    public string? Model { get; set; }


    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VehicleStatus Status { get; set; } = VehicleStatus.Active;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Company? Company { get; set; }
    public Driver? CurrentDriver { get; set; } // Optional navigation
    public ICollection<Device> Devices { get; set; } = new List<Device>();
    public ICollection<VehicleAssignment> VehicleAssignments { get; set; } = new List<VehicleAssignment>();
    public ICollection<SafetyEvent> SafetyEvents { get; set; } = new List<SafetyEvent>();
    public ICollection<TelemetryEvent> TelemetryEvents { get; set; } = new List<TelemetryEvent>();
}
