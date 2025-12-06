using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Vehicles;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace AegisDrive.Api.Features.Monitoring;

public static class GetVehicleLiveState
{
    
   

    public record Query(int VehicleId) : IRequest<Result<VehicleLiveStateResponse>>;

    internal sealed class Handler : IRequestHandler<Query, Result<VehicleLiveStateResponse>>
    {
        private readonly IGenericRepository<Vehicle, int> _vehicleRepository;
        private readonly IDatabase _redis;

        public Handler(IGenericRepository<Vehicle, int> vehicleRepository, IConnectionMultiplexer redisMux)
        {
            _vehicleRepository = vehicleRepository;
            _redis = redisMux.GetDatabase();
        }

        public async Task<Result<VehicleLiveStateResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            string redisKey = $"vehicle:{request.VehicleId}:live";

            //  Check Redis Cache
            var cachedData = await _redis.StringGetAsync(redisKey);
            if (!cachedData.IsNull)
            {
                var liveState = JsonSerializer.Deserialize<VehicleLiveStateResponse>(cachedData.ToString());
                if (liveState != null)
                {
                    return Result.Success(liveState);
                }
            }

            // We need to fetch the Vehicle AND its MOST RECENT telemetry point.            
            var vehicleDto = await _vehicleRepository.GetAll()
                .AsNoTracking()
                .Where(v => v.Id == request.VehicleId)
                .Select(v => new
                {
                    v.Id,
                    v.PlateNumber,
                    v.Status,
                    LatestTelemetry = v.TelemetryEvents
                        .OrderByDescending(t => t.Timestamp)
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (vehicleDto is null)
            {
                return Result.Failure<VehicleLiveStateResponse>(new Error("Vehicle.NotFound", $"Vehicle with ID {request.VehicleId} was not found."));
            }

            var response = new VehicleLiveStateResponse(
                vehicleDto.Id,
                vehicleDto.PlateNumber ?? "N/A",
                vehicleDto.Status.ToString(),
                vehicleDto.LatestTelemetry != null
                    ? new LiveLocationResponse(
                        vehicleDto.LatestTelemetry.Latitude,
                        vehicleDto.LatestTelemetry.Longitude,
                        vehicleDto.LatestTelemetry.SpeedKmh,
                        vehicleDto.LatestTelemetry.Timestamp)
                    : new LiveLocationResponse(0, 0, 0, DateTime.UtcNow) // Default / Placeholder
            );

            var serializedData = JsonSerializer.Serialize(response);
            await _redis.StringSetAsync(redisKey, serializedData, TimeSpan.FromSeconds(60));

            return Result.Success(response);
        }
    }

  
}