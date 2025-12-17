using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Vehicles;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AegisDrive.Api.Features.Vehicles;

public static class StartShift
{
    public record Command(int DriverId, int VehicleId, string OwnerUserId) : ICommand<Result<StartShiftResponse>>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.DriverId).GreaterThan(0);
            RuleFor(x => x.VehicleId).GreaterThan(0);
            RuleFor(x => x.OwnerUserId).NotEmpty(); 
        }
    }

    internal sealed class Handler : IRequestHandler<Command, Result<StartShiftResponse>>
    {
        private readonly IGenericRepository<Vehicle, int> _vehicleRepository;
        private readonly IGenericRepository<VehicleAssignment, long> _assignmentRepository;
        private readonly IValidator<Command> _validator;

        public Handler(
            IGenericRepository<Vehicle, int> vehicleRepository,
            IGenericRepository<VehicleAssignment, long> assignmentRepository,
            IValidator<Command> validator)
        {
            _vehicleRepository = vehicleRepository;
            _assignmentRepository = assignmentRepository;
            _validator = validator;
        }

        public async Task<Result<StartShiftResponse>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await _validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
                return Result.Failure<StartShiftResponse>(new Error("Validation", validationResult.ToString()));

            var targetVehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId);
            if (targetVehicle == null)
                return Result.Failure<StartShiftResponse>(new Error("Vehicle.NotFound", "Vehicle not found."));

            if (targetVehicle.Status != VehicleStatus.Active)
                return Result.Failure<StartShiftResponse>(new Error("Vehicle.Unavailable", $"Vehicle is {targetVehicle.Status} and cannot be assigned."));

            var now = DateTime.UtcNow;

            // C. LOGIC 1: Is this DRIVER already driving ANOTHER vehicle?
            // If so, we must Clock Out of that old vehicle AND clear its OwnerUserId.
            var activeDriverShift = await _assignmentRepository.GetAll()
                .Where(va => va.DriverId == request.DriverId && va.UnassignedAt == null)
                .FirstOrDefaultAsync(cancellationToken);

            if (activeDriverShift != null)
            {
                // 1. End the old shift
                activeDriverShift.UnassignedAt = now;
                _assignmentRepository.SaveInclude(activeDriverShift, nameof(VehicleAssignment.UnassignedAt));

                // 2. Clear the OLD vehicle's links
                var oldVehicle = await _vehicleRepository.GetByIdAsync(activeDriverShift.VehicleId);
                if (oldVehicle != null)
                {
                    oldVehicle.CurrentDriverId = null;
                    oldVehicle.OwnerUserId = null; 
                    
                    _vehicleRepository.SaveInclude(oldVehicle, nameof(Vehicle.CurrentDriverId), nameof(Vehicle.OwnerUserId));
                }
            }

            // D. LOGIC 2: Does the TARGET VEHICLE already have a DIFFERENT driver?
            // If so, force-end that driver's shift.
            var activeVehicleShift = await _assignmentRepository.GetAll()
                .Where(va => va.VehicleId == request.VehicleId && va.UnassignedAt == null)
                .FirstOrDefaultAsync(cancellationToken);

            if (activeVehicleShift != null)
            {
                activeVehicleShift.UnassignedAt = now;
                _assignmentRepository.SaveInclude(activeVehicleShift, nameof(VehicleAssignment.UnassignedAt));
            }

            // E. Create New Assignment
            var newAssignment = new VehicleAssignment
            {
                DriverId = request.DriverId,
                VehicleId = request.VehicleId,
                AssignedAt = now,
                UnassignedAt = null
            };

            await _assignmentRepository.AddAsync(newAssignment);

            // F. Update Target Vehicle Fast Lookup
            targetVehicle.CurrentDriverId = request.DriverId;
            targetVehicle.OwnerUserId = request.OwnerUserId; //Link to logged-in user

            _vehicleRepository.SaveInclude(targetVehicle, nameof(Vehicle.CurrentDriverId), nameof(Vehicle.OwnerUserId));

            // G. Final Save
            await _assignmentRepository.SaveChangesAsync();

            return Result.Success(new StartShiftResponse(
                newAssignment.Id,
                newAssignment.DriverId,
                newAssignment.VehicleId,
                newAssignment.AssignedAt,
                "Shift started successfully."
            ));
        }
    }
    
}