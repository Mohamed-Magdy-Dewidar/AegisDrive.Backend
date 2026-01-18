using AegisDrive.Api.Contracts;
using AegisDrive.Api.DataBase;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AegisDrive.Api.Features.Drivers;

public static class UpdateDriverGlobalScore
{
    public record Command(int DriverId, double LastTripScore) : ICommand<Result<double>>;

    internal sealed class Handler(IGenericRepository<Driver, int> driverRepository, AppDbContext context)
        : IRequestHandler<Command, Result<double>>
    {
        public async Task<Result<double>> Handle(Command request, CancellationToken cancellationToken)
        {
            var driver = await driverRepository.GetByIdAsync(request.DriverId);
            if (driver == null) return Result<double>.Failure<double>(new Error("Driver.NotFound", "Driver not found."));

            // Calculate new average from the database
            // Formula: Average SafetyScore of all 'Completed' trips for this driver
            var averageScore = await context.Set<Trip>()
                .Where(t => t.DriverId == request.DriverId && t.Status == TripStatus.Completed)
                .AverageAsync(t => (double?)t.TripSafetyScore, cancellationToken) ?? 100.0;

            driver.SafetyScore = (int)Math.Round(averageScore);
            driverRepository.SaveInclude(driver , [nameof(driver.SafetyScore)]);

            return Result<double>.Success(averageScore);
        }
    }
}