using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Vehicles; // Assuming EndShiftRequest is here
using AegisDrive.Api.DataBase;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Shared;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using Carter;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AegisDrive.Api.Features.Vehicles;

public static class EndShift
{
    public record Command(int VehicleId) : ICommand<Result<EndShiftResponse>>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.VehicleId).GreaterThan(0)
                .WithMessage("Vehicle Id must be greater than Zero");
        }
    }

    

    internal sealed class Handler : IRequestHandler<Command, Result<EndShiftResponse>>
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

        public async Task<Result<EndShiftResponse>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await _validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
                return Result.Failure<EndShiftResponse>(new Error("Validation", validationResult.ToString()));

            var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId);
            if (vehicle == null)
                return Result.Failure<EndShiftResponse>(new Error("Vehicle.NotFound", "Vehicle not found."));

            // C. Logic: Find the ACTIVE assignment for this vehicle
            var activeAssignment = await _assignmentRepository
                .GetAll(va => va.VehicleId == request.VehicleId && va.UnassignedAt == null)
                .FirstOrDefaultAsync(cancellationToken);

            if (activeAssignment == null)
            {
                return Result.Failure<EndShiftResponse>(new Error("Shift.NotFound", "No active shift found for this vehicle."));
            }

            var now = DateTime.UtcNow;

            // D. End the Assignment Record
            activeAssignment.UnassignedAt = now;
            _assignmentRepository.SaveInclude(activeAssignment, nameof(VehicleAssignment.UnassignedAt));

            // E. Clear Vehicle's Links (Fast Lookup)
            vehicle.CurrentDriverId = null;
            vehicle.OwnerUserId = null;

            // Explicitly save the changes to these columns
            _vehicleRepository.SaveInclude(vehicle, nameof(Vehicle.CurrentDriverId), nameof(Vehicle.OwnerUserId));

            // G. Final Save
            await _assignmentRepository.SaveChangesAsync();

            // Calculate duration for display
            var duration = now - activeAssignment.AssignedAt;
            string durationStr = $"{duration.Hours}h {duration.Minutes}m";

            return Result.Success(new EndShiftResponse(
                activeAssignment.Id,
                activeAssignment.DriverId,
                activeAssignment.VehicleId,
                activeAssignment.AssignedAt,
                now,
                durationStr
            ));
        }
    }

    
}