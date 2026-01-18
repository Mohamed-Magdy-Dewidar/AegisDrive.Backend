using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Vehicles;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace AegisDrive.Api.Features.Vehicles;

public static class GetVehicleLatestCoordinates
{

    public record Query(int VehicleId) : IRequest<Result<GpsCoordinate>>;

    public record GpsCoordinate(double Latitude, double Longitude);

    internal sealed class Handler : IRequestHandler<Query, Result<GpsCoordinate>>
    {
        private readonly IGenericRepository<TelemetryEvent, Guid> _telemetryEventRepository;
        private readonly IDatabase _redis;

        public Handler(IGenericRepository<TelemetryEvent, Guid> telemetryEventRepository, IConnectionMultiplexer redisMux)
        {
            _telemetryEventRepository = telemetryEventRepository;
            _redis = redisMux.GetDatabase();
        }
        public async Task<Result<GpsCoordinate>> Handle(Query request, CancellationToken cancellationToken)
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
                if (dict.TryGetValue("Longitude", out var Longitude))
                {
                    var liveLocation = new GpsCoordinate(
                        double.Parse(dict.GetValueOrDefault("Latitude", "0")),
                        Longitude is not null ? double.Parse(Longitude) : 0
                    );

                    return Result.Success(liveLocation);
                }
            }

            var GpsData = await _telemetryEventRepository.GetAll()
           .Where(te => te.VehicleId == request.VehicleId)
           .OrderByDescending(te => te.Timestamp)
           .Select(v => new GpsCoordinate(
               v.Latitude, 
               v.Longitude
            ))
           .FirstOrDefaultAsync(cancellationToken);

            if (GpsData is null)
            {
                return Result.Failure<GpsCoordinate>(new Error("GpsCoordinate.NotFound", $"GpsCoordinate for Vehicle with ID {request.VehicleId} was not found."));
            }

            return Result.Success(GpsData);
        }
    }
}
