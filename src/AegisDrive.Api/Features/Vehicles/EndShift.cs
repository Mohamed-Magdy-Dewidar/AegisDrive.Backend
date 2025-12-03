using AegisDrive.Api.Contracts;
using AegisDrive.Api.DataBase;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Shared;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using Carter;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AegisDrive.Api.Features.Fleet;

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

    public record EndShiftResponse(long AssignmentId, int DriverId, int VehicleId, DateTime StartTime, DateTime EndTime, string Duration)
    {
        public string Message { get; init; } = $"Shift ended successfully. Duration: {Duration}";
    }

   
    internal sealed class Handler : IRequestHandler<Command, Result<EndShiftResponse>>
    {
        private readonly IGenericRepository<Vehicle, int> _vehicleRepository;
        private readonly IGenericRepository<VehicleAssignment, long> _assignmentRepository;
        private readonly IValidator<Command> _validator;

        public Handler(IGenericRepository<Vehicle, int> vehicleRepository,IGenericRepository<VehicleAssignment, long> assignmentRepository,IValidator<Command> validator)
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
                // If no active shift found, we can't end it.
                // Optionally, we could just return Success if the goal is "ensure it's stopped", 
                // but returning Failure helps the frontend know the state was inconsistent.
                return Result.Failure<EndShiftResponse>(new Error("Shift.NotFound", "No active shift found for this vehicle."));
            }


            var now = DateTime.UtcNow;

            activeAssignment.UnassignedAt = now;
            _assignmentRepository.SaveInclude(activeAssignment, nameof(VehicleAssignment.UnassignedAt));

            // E. Clear Vehicle's CurrentDriverId (Fast Lookup)
            vehicle.CurrentDriverId = null;
            _vehicleRepository.SaveInclude(vehicle, nameof(Vehicle.CurrentDriverId));

            

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