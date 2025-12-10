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
            // A. CHECK REDIS (Hash Strategy)
            HashEntry[] hashEntries = await _redis.HashGetAllAsync(redisKey);            
            
            if (hashEntries.Any())
            {
                // Convert Hash -> DTO
                var dict = hashEntries.ToDictionary(h => h.Name.ToString(), h => h.Value.ToString());
                // If we have at least the VehicleId, we consider it a Cache Hit
                if (dict.TryGetValue("PlateNumber", out var plateNumber))
                {
                    string Status = dict.GetValueOrDefault("Status", "Active");
                    var liveLocation = new LiveLocationResponse(
                        double.Parse(dict.GetValueOrDefault("Latitude", "0")),
                        double.Parse(dict.GetValueOrDefault("Longitude", "0")),
                        double.Parse(dict.GetValueOrDefault("SpeedKmh", "0")),
                        DateTime.Parse(dict.GetValueOrDefault("LastUpdateUtc", DateTime.UtcNow.ToString("o")))
                    );

                    return Result.Success(new VehicleLiveStateResponse(
                        request.VehicleId,
                        plateNumber,
                        Status,
                        liveLocation
                    ));
                }
            }


            // We need to fetch the Vehicle AND its MOST RECENT telemetry point.            
            var vehicleDto = await _vehicleRepository.GetAll()
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
                vehicleDto.PlateNumber ?? "N/A" ,
                vehicleDto.Status.ToString(),
                vehicleDto.LatestTelemetry != null
                    ? new LiveLocationResponse(
                        vehicleDto.LatestTelemetry.Latitude,
                        vehicleDto.LatestTelemetry.Longitude,
                        vehicleDto.LatestTelemetry.SpeedKmh,
                        vehicleDto.LatestTelemetry.Timestamp)
                    : new LiveLocationResponse(0, 0, 0, DateTime.UtcNow) 
            );
            
            // C. UPDATE REDIS (Using HashSetAsync to match IngestTelemetry)
            var newHashEntries = new HashEntry[]
            {
                new HashEntry("PlateNumber", response.PlateNumber),
                new HashEntry("Status",   response.Status),
                new HashEntry("Latitude", response.LiveLocation?.Latitude ?? 0),
                new HashEntry("Longitude", response.LiveLocation?.Longitude ?? 0),
                new HashEntry("SpeedKmh", response.LiveLocation?.SpeedKmh ?? 0),
                new HashEntry("LastUpdateUtc", response.LiveLocation?.LastUpdateUtc.ToString("o") ?? DateTime.UtcNow.ToString("o"))
            };

            await _redis.HashSetAsync(redisKey, newHashEntries);
            await _redis.KeyExpireAsync(redisKey, TimeSpan.FromMinutes(2));


            return Result.Success(response);
        }
    }

  
}