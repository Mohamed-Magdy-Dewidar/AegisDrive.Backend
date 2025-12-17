using AegisDrive.Api.Contracts;
using AegisDrive.Api.DataBase;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AegisDrive.Api.Features.Vehicles;


public static class UpdateVehicle
{
    
    public record Command(int VehicleId, string PlateNumber, string Model, VehicleStatus Status) : ICommand<Result<int>>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.VehicleId).GreaterThan(0);
            RuleFor(x => x.PlateNumber).NotEmpty();
            RuleFor(x => x.Model).NotEmpty();
        }
    }

    internal sealed class Handler : IRequestHandler<Command, Result<int>>
    {
        private readonly IGenericRepository<Vehicle , int> _vehicleRepository;
        private readonly Validator _validator;
        public Handler(IGenericRepository<Vehicle, int>  vehicleRepository, Validator validator)
        {
            _vehicleRepository = vehicleRepository;
            _validator = validator;
        }

        public async Task<Result<int>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await _validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                var error = new Error("Validation Failed", string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
                return Result.Failure<int>(error); 
            }

            var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId);
            if (vehicle == null)
                return Result.Failure<int>(new Error("Vehicle.NotFound", "Vehicle not found"));

            var existingVehicle = await _vehicleRepository.GetAll(v => v.PlateNumber == request.PlateNumber && v.Id != request.VehicleId)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingVehicle != null)
                return Result.Failure<int>(new Error("Vehicle.DuplicatePlate", "A vehicle with this plate number already exists"));


            vehicle.PlateNumber = request.PlateNumber;
            vehicle.Model = request.Model;
            vehicle.Status = request.Status;

            string[] valuesToBeUpdated = [nameof(vehicle.PlateNumber), nameof(vehicle.Model), nameof(vehicle.Status)];
             _vehicleRepository.SaveInclude(vehicle, valuesToBeUpdated);

            return Result.Success(vehicle.Id);
        }
    }
}
