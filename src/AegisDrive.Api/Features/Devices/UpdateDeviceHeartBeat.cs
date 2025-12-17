using AegisDrive.Api.Contracts;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums.Device;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;

namespace AegisDrive.Api.Features.Devices;

public static class UpdateDeviceHeartBeat
{
    public record Command(string DeviceId) : ICommand<Result>;

    internal sealed class Handler : IRequestHandler<Command, Result>
    {
        private readonly IGenericRepository<Device, string> _deviceRepository;

        public Handler(IGenericRepository<Device, string> deviceRepository)
        {
            _deviceRepository = deviceRepository;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var device = await _deviceRepository.GetByIdAsync(request.DeviceId);

            if (device is null)
                return Result.Failure(new Error("DeviceNotFound", "The specified device was not found."));

            bool isModified = false;

            if (device.Status != DeviceStatus.Online)
            {
                device.Status = DeviceStatus.Online;
                isModified = true;
            }

            device.LastHeartbeat = DateTime.UtcNow;

            if (isModified)
            {
                _deviceRepository.SaveInclude(device, nameof(Device.Status), nameof(Device.LastHeartbeat));
            }
            else
            {
                _deviceRepository.SaveInclude(device, nameof(Device.LastHeartbeat));
            }

            await _deviceRepository.SaveChangesAsync();
            return Result.Success();
        }
    }
}