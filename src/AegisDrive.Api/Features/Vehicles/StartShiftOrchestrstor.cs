using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Vehicles; 
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums; 
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;

namespace AegisDrive.Api.Features.Fleet;

public static class StartShift
{
    
    public record Command(int DriverId, int VehicleId) : ICommand<Result<StartShiftResponse>>;

   
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.DriverId).GreaterThan(0);
            RuleFor(x => x.VehicleId).GreaterThan(0);
        }
    }

    
   
    internal sealed class Handler : IRequestHandler<Command, Result<StartShiftResponse>>
    {
        private readonly IGenericRepository<Vehicle, int> _vehicleRepository;
        private readonly IGenericRepository<VehicleAssignment, long> _assignmentRepository;
        private readonly IValidator<Command> _validator;

        public Handler( IGenericRepository<Vehicle, int> vehicleRepository, IGenericRepository<VehicleAssignment, long> assignmentRepository, IValidator<Command> validator)
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

            if (targetVehicle.Status != VehicleStatus.Active) // Assuming Enum comparison
                return Result.Failure<StartShiftResponse>(new Error("Vehicle.Unavailable", $"Vehicle is {targetVehicle.Status} and cannot be assigned."));

            var now = DateTime.UtcNow;

            // C. LOGIC 1: Is this DRIVER already driving ANOTHER vehicle?
            // If so, we must Clock Out of that old vehicle.
            var activeDriverShift = await _assignmentRepository.GetAll()
                .Where(va => va.DriverId == request.DriverId && va.UnassignedAt == null)
                .FirstOrDefaultAsync(cancellationToken);

            if (activeDriverShift != null)
            {
                // 1. End the old shift
                activeDriverShift.UnassignedAt = now;
                _assignmentRepository.SaveInclude(activeDriverShift, nameof(VehicleAssignment.UnassignedAt));

                // 2. CRITICAL FIX: Clear the OLD vehicle's CurrentDriverId
                // We must fetch the OLD vehicle using the assignment's VehicleId, NOT the request.DriverId
                var oldVehicle = await _vehicleRepository.GetByIdAsync(activeDriverShift.VehicleId);
                if (oldVehicle != null)
                {
                    oldVehicle.CurrentDriverId = null;
                    _vehicleRepository.SaveInclude(oldVehicle, nameof(Vehicle.CurrentDriverId));
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
            _vehicleRepository.SaveInclude(targetVehicle, nameof(Vehicle.CurrentDriverId));

            // G. Final Save (Transactional Middleware handles the atomic commit)
            await _assignmentRepository.SaveChangesAsync();

            return Result.Success(new StartShiftResponse(newAssignment.Id,newAssignment.DriverId,newAssignment.VehicleId,newAssignment.AssignedAt,"Shift started successfully."));
        

        }
    }



}