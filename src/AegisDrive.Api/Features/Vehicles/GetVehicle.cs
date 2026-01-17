using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Vehicles;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace AegisDrive.Api.Features.Vehicles;

public static class GetVehicle
{

    public record Query(int? Id ) : IRequest<Result<GetVehicleResponse>>;

    internal sealed class Handler : IRequestHandler<Query, Result<GetVehicleResponse>>
    {
        private readonly IGenericRepository<Vehicle, int> _vehicleRepository;
        private readonly IDatabase _redis;

        public Handler(IGenericRepository<Vehicle, int> vehicleRepository, IConnectionMultiplexer connectionMultiplexer)
        {
            _vehicleRepository = vehicleRepository;
            _redis = connectionMultiplexer.GetDatabase();
        }


        //  Cache Aside Pattern
        public async Task<Result<GetVehicleResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            string cacheKey = $"vehicle:{request.Id}:details";

            var cachedData = await _redis.StringGetAsync(cacheKey);            
            if (!cachedData.IsNull)
            {
                var cachedResponse = JsonSerializer.Deserialize<GetVehicleResponse>(cachedData.ToString());
                if (cachedResponse != null)
                {
                    return Result.Success(cachedResponse);
                }
            }

            var vehicleDto = await _vehicleRepository.GetAll()
                .AsNoTracking()
                .Where(v => v.Id == request.Id)
                .Select(v => new GetVehicleResponse(
                    v.Id,
                    v.PlateNumber ?? "N/A",
                    v.Model,
                    v.Status.ToString(), 
                    v.CurrentDriverId,
                    v.CompanyId,
                    v.OwnerUserId
                ))
                .FirstOrDefaultAsync(cancellationToken);

            if (vehicleDto is null)
            {
                return Result.Failure<GetVehicleResponse>(new Error("Vehicle.NotFound", $"Vehicle with ID {request.Id} was not found."));
            }

            // C. Save to Cache (Expiry: 10 minutes)
            var serializedData = JsonSerializer.Serialize(vehicleDto);
            await _redis.StringSetAsync(cacheKey, serializedData, TimeSpan.FromMinutes(10));

            return Result.Success(vehicleDto);
        }
    }
}