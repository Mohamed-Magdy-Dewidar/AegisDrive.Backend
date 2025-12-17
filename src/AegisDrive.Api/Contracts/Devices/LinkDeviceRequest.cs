using AegisDrive.Api.Entities.Enums.Device;
namespace AegisDrive.Api.Contracts.Devices;


public record LinkDeviceRequest(string DeviceId, DeviceType Type);