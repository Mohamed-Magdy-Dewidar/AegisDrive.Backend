using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums.Device;
using System.Text.Json.Serialization;

namespace AegisDrive.Api.Entities;

public class Device : BaseEntity<string>
{
    public int? VehicleId { get; set; }


    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DeviceType Type { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DeviceStatus Status { get; set; } = DeviceStatus.Online;


    public DateTime? LastHeartbeat { get; set; }
    public string? FirmwareVersion { get; set; }

    public Vehicle? Vehicle { get; set; }
}
