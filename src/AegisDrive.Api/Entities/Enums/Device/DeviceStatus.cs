namespace AegisDrive.Api.Entities.Enums.Device;


public enum DeviceStatus
{
    Offline,
    Online,
    Inactive,   // Hasn't reported in > 24 hours
    Maintenance // Manually set by admin
}
