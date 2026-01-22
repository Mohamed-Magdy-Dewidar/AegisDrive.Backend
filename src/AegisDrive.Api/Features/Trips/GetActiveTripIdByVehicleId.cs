using AegisDrive.Api.Contracts;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace AegisDrive.Api.Features.Trips;

public static class GetActiveTripIdByVehicleId
{
    public record Query(int VehicleId) : IRequest<Result<GetActiveTripIdByVehicleIdResponse>>;


    public record GetActiveTripIdByVehicleIdResponse(Guid? Id);


    internal sealed class Handler : IRequestHandler<Query, Result<GetActiveTripIdByVehicleIdResponse>>
    {
        private readonly IDatabase _redis;
        private readonly IGenericRepository<Trip, Guid> _tripRepository;
        public Handler(IConnectionMultiplexer connectionMultiplexer, IGenericRepository<Trip, Guid> tripRepository)
        {
            _redis = connectionMultiplexer.GetDatabase(); 
            _tripRepository = tripRepository;
        }
 
        public async Task<Result<GetActiveTripIdByVehicleIdResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            // active_vehicle:driver:17
            var activeTripKey = $"active_trip:vehicle:{request.VehicleId}";
            var existingTrip = await _redis.StringGetAsync(activeTripKey);

            if (!existingTrip.IsNull)
            {
                var tripId = Guid.Parse(existingTrip!);
                return Result.Success<GetActiveTripIdByVehicleIdResponse>(new GetActiveTripIdByVehicleIdResponse(tripId));
            }

            var Trip = await _tripRepository.GetAll()
                .Where(t => t.VehicleId == request.VehicleId && t.EndTime == null)
                .Select(t => new { t.Id })
                .FirstOrDefaultAsync(cancellationToken);

            if (Trip != null)
            {
                return Result.Success<GetActiveTripIdByVehicleIdResponse>(new GetActiveTripIdByVehicleIdResponse(Trip.Id));
            }

            return Result.Failure<GetActiveTripIdByVehicleIdResponse>(new Error("Trip.GetTripId", "Trip Id does not exists."));
            
        }
    }
}
