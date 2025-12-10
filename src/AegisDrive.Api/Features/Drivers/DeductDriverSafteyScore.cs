using AegisDrive.Api.Contracts;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;

namespace AegisDrive.Api.Features.Drivers;

public static class DeductDriverSafteyScore
{
    public record Command(int DriverId, AlertLevel AlertLevel) : ICommand<Result<double>>;

    internal sealed class Handler : IRequestHandler<Command, Result<double>>
    {
        private readonly IGenericRepository<Driver, int> _driverRepo;

        public Handler(IGenericRepository<Driver, int> driverRepo)
        {
            _driverRepo = driverRepo;
        }

        public async Task<Result<double>> Handle(Command request, CancellationToken cancellationToken)
        {
            var driver = await _driverRepo.GetByIdAsync(request.DriverId);
            if (driver == null) return Result<double>.Failure<double>(new Error("Driver.NotFound", "Driver not found"));


            int deduction = request.AlertLevel switch
            {
                AlertLevel.CRITICAL => 10,
                AlertLevel.HIGH => 5,
                AlertLevel.MEDIUM => 2,
                _ => 0
            };

            if (deduction > 0)
            {
                driver.SafetyScore = Math.Max(0, driver.SafetyScore - deduction);
                // Save specific field
                _driverRepo.SaveInclude(driver, nameof(Driver.SafetyScore));
                await _driverRepo.SaveChangesAsync(cancellationToken);
            }

            return Result<double>.Success<double>(driver.SafetyScore);
        }
    }
}
