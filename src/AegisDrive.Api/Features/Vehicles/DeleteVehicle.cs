using AegisDrive.Api.Contracts;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using Carter;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AegisDrive.Api.Features.Vehicles;

public static class DeleteVehicle
{
    public record Command(int VehicleId) : ICommand<Result<string>>;

    internal sealed class Handler : IRequestHandler<Command, Result<string>>
    {
        private readonly IGenericRepository<Vehicle, int> _vehicleRepo;
        public Handler(IGenericRepository<Vehicle, int> vehicleRepo)
        {
            _vehicleRepo = vehicleRepo;
        }

        public async Task<Result<string>> Handle(Command request, CancellationToken cancellationToken)
        {
            var vehicle = await _vehicleRepo.GetAll(v => v.Id == request.VehicleId)
                .Select(V => new
                {
                    V.Id,
                    V.CurrentDriverId
                }).FirstOrDefaultAsync();

            if (vehicle == null)
                return Result.Failure<string>(new Error("Vehicle.NotFound", "Vehicle not found"));

            if (vehicle.CurrentDriverId.HasValue)
                return Result.Failure<string>(new Error("Vehicle.Busy", "Cannot delete a vehicle while it is assigned to a driver. End the shift first."));


            await _vehicleRepo.DeleteAsync(vehicle.Id); // Soft delete using repository

            return Result.Success<string>("Vehicle Deleted SuccessFully");
        }
    }
    
}