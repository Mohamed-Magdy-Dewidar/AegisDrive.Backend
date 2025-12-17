using AegisDrive.Api.Contracts;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace AegisDrive.Api.Features.Devices;

public static class GetDeviceById
{
    public record Query(string DeviceId) : IRequest<Result<DeviceContextCache>>;

    public record DeviceContextCache(int VehicleId, int? CompanyId, string? OwnerUserId, string PlateNumber, string Status);

    internal sealed class Handler : IRequestHandler<Query, Result<DeviceContextCache>>
    {
        private readonly IGenericRepository<Device, string> _deviceRepo;
        private readonly IDatabase _redis;

        public Handler(IGenericRepository<Device, string> deviceRepo, IConnectionMultiplexer redisMux)
        {
            _deviceRepo = deviceRepo;
            _redis = redisMux.GetDatabase();
        }

        public async Task<Result<DeviceContextCache>> Handle(Query request, CancellationToken cancellationToken)
        {
            // --- A. CACHE CHECK ---
            string mappingKey = $"device:{request.DeviceId}:map";

            var cachedMapping = await _redis.StringGetAsync(mappingKey);
            if (!cachedMapping.IsNull)
            {
                var cachedContext = JsonSerializer.Deserialize<DeviceContextCache>(cachedMapping.ToString());
                if (cachedContext != null)
                {
                    // FIXED: Return immediately if found in cache
                    return Result.Success(cachedContext);
                }
            }

            // --- B. DB FETCH (Cache Miss) ---
            var deviceDto = await _deviceRepo.GetAll()
                .AsNoTracking()
                .Where(d => d.Id == request.DeviceId)
                .Select(d => new
                {
                    d.VehicleId,
                    d.Vehicle!.CompanyId,
                    d.Vehicle.OwnerUserId,
                    d.Vehicle.PlateNumber,
                    d.Vehicle.Status
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (deviceDto == null || deviceDto.VehicleId == null)
                return Result.Failure<DeviceContextCache>(new Error("Device.Unknown", "Device not linked or not found"));

            var context = new DeviceContextCache(
                deviceDto.VehicleId.Value,
                deviceDto.CompanyId,
                deviceDto.OwnerUserId?.ToLower(),
                deviceDto.PlateNumber ?? "Unknown",
                deviceDto.Status.ToString());

            // Cache for 1 hour
            await _redis.StringSetAsync(mappingKey, JsonSerializer.Serialize(context), TimeSpan.FromHours(1));

            return Result.Success(context);
        }
    }
}