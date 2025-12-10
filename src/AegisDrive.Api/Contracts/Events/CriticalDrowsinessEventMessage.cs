using System.Text.Json.Serialization;
namespace AegisDrive.Api.Contracts.Events;

public class CriticalDrowsinessEventMessage
{
    [JsonPropertyName("event_id")]
    public Guid EventId { get; set; }

    // ✅ FIX: Make timestamp nullable string to handle custom format
    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("vehicle_id")]
    public int VehicleId { get; set; }

    [JsonPropertyName("state")]
    public string DriverState { get; set; } = string.Empty;

    [JsonPropertyName("alert_level")]
    public string AlertLevel { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("ear")]
    public double EarValue { get; set; }

    [JsonPropertyName("mar")]
    public double MarValue { get; set; }

    [JsonPropertyName("head_yaw")]
    public double HeadYaw { get; set; }

    // ✅ FIX: Changed from s3_driver_image_path to s3_driver_image
    [JsonPropertyName("s3_driver_image")]
    public string? S3DriverImagePath { get; set; }

    // ✅ FIX: Changed from s3_road_image_path to s3_road_image
    [JsonPropertyName("s3_road_image")]
    public string? S3RoadImagePath { get; set; }

    [JsonPropertyName("road_status")]
    public RoadStatusInfo RoadStatus { get; set; } = new();

    public class RoadStatusInfo
    {
        [JsonPropertyName("has_hazard")]
        public bool HasHazard { get; set; }

        [JsonPropertyName("vehicle_count")]
        public int VehicleCount { get; set; }

        [JsonPropertyName("pedestrian_count")]
        public int PedestrianCount { get; set; }

        [JsonPropertyName("closest_object_distance")]
        public double ClosestObjectDistance { get; set; }
    }

    // ✅ Helper method to parse the custom timestamp
    public DateTime GetParsedTimestamp()
    {
        // Parse "Dec06_2025_04h03m11s" format
        if (DateTime.TryParseExact(Timestamp,
            "MMMdd_yyyy_HHhmm'ss'",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out DateTime result))
        {
            return result;
        }

        // Fallback to current time if parsing fails
        return DateTime.UtcNow;
    }
}