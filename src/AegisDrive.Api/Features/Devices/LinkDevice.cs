using AegisDrive.Api.Contracts;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums.Device;
using AegisDrive.Api.Features.Vehicles; // For GetVehicle Query
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using Carter;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AegisDrive.Api.Features.Devices;

public static class LinkDevice
{

    

    public record Command(int VehicleId, string DeviceId, DeviceType Type) : ICommand<Result<string>>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.VehicleId).GreaterThan(0);
            RuleFor(x => x.DeviceId).NotEmpty().MinimumLength(3);
            RuleFor(x => x.Type).IsInEnum();
        }
    }

    internal sealed class Handler : IRequestHandler<Command, Result<string>>
    {
        private readonly IGenericRepository<Device, string> _deviceRepo;
        private readonly ISender _sender;

        public Handler(IGenericRepository<Device, string> deviceRepo, ISender sender)
        {
            _deviceRepo = deviceRepo;
            _sender = sender;
        }

        public async Task<Result<string>> Handle(Command request, CancellationToken cancellationToken)
        {
            var vehicleCheck = await _sender.Send(new GetVehicle.Query(request.VehicleId), cancellationToken);

            if (vehicleCheck.IsFailure)
            {
                return Result.Failure<string>(new Error("Vehicle.NotFound", $"Vehicle with ID {request.VehicleId} not found."));
            }

            var device = await _deviceRepo.GetByIdAsync(request.DeviceId);

            if (device == null)
            {
                device = new Device
                {
                    Id = request.DeviceId,
                    VehicleId = request.VehicleId,
                    Type = request.Type,
                    Status = DeviceStatus.Online, // Default status
                    LastHeartbeat = DateTime.UtcNow,
                    FirmwareVersion = "1.0.0" // Default firmware
                };

                await _deviceRepo.AddAsync(device);
            }
            else
            {
                // If it belongs to another vehicle, this reassigns it (Foreign Key Update)
                // Move Existing Device
                device.VehicleId = request.VehicleId;
                device.Type = request.Type; 
                device.Status = DeviceStatus.Online;
                device.LastHeartbeat = DateTime.UtcNow;

                _deviceRepo.Update(device);
            }

            await _deviceRepo.SaveChangesAsync();
            return Result.Success($"Device {request.DeviceId} ({request.Type}) successfully linked to Vehicle ID {request.VehicleId}");
        }
    }
    
}