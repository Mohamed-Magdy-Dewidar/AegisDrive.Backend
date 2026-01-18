using AegisDrive.Api.Contracts;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis; 
using System.Text.Json;    

namespace AegisDrive.Api.Features.Vehicles;

public static class GetVehicleIdByDriverId
{
    public record Query(int DriverId) : IRequest<Result<GetVehicleIdByDriverIdResponse>>;

    public record GetVehicleIdByDriverIdResponse(int VehicleId);

    internal sealed class Handler : IRequestHandler<Query, Result<GetVehicleIdByDriverIdResponse>>
    {
        private readonly IGenericRepository<Vehicle, int> _vehicleRepository;
        private readonly IDatabase _redisDatabase;
        private const string CacheKeyPrefix = "active_vehicle:driver:";

        public Handler(IGenericRepository<Vehicle, int> vehicleRepository, IConnectionMultiplexer redis)
        {
            _vehicleRepository = vehicleRepository;
            _redisDatabase = redis.GetDatabase(); 
        }

        public async Task<Result<GetVehicleIdByDriverIdResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            string cacheKey = $"{CacheKeyPrefix}{request.DriverId}";
            var cachedData = await _redisDatabase.StringGetAsync(cacheKey);
            if (cachedData.HasValue)
            {
                var cachedResponse = JsonSerializer.Deserialize<GetVehicleIdByDriverIdResponse>(cachedData!);
                return Result<GetVehicleIdByDriverIdResponse>.Success(cachedResponse!);
            }

            var vehicle = await _vehicleRepository.GetAll()
                .Where(v => v.CurrentDriverId == request.DriverId)
                .Select(x => new
                {
                    VehicleId = x.Id
                }).FirstOrDefaultAsync(cancellationToken);

            if (vehicle is null)
                return Result<GetVehicleIdByDriverIdResponse>.Failure<GetVehicleIdByDriverIdResponse>(
                    new Error("Vehicle.NotFound", "No vehicle found for the specified driver."));
            var response = new GetVehicleIdByDriverIdResponse(vehicle.VehicleId);


            var serializedResponse = JsonSerializer.Serialize(response);
            await _redisDatabase.StringSetAsync(
                cacheKey,
                serializedResponse,
                TimeSpan.FromMinutes(30));

            return Result<GetVehicleIdByDriverIdResponse>.Success(response);
        }
    }
}