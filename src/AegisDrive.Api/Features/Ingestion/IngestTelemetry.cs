using AegisDrive.Api.Contracts;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace AegisDrive.Api.Features.Ingestion;

public static class IngestTelemetry
{

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.DeviceId).NotEmpty();
            RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
            RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
            RuleFor(x => x.SpeedKmh).GreaterThanOrEqualTo(0);
            RuleFor(x => x.EventType).NotEmpty();
        }
    }

    public record Command(string DeviceId, double Latitude, double Longitude, double SpeedKmh, string EventType, DateTime Timestamp) : ICommand<Result>;

    private record DeviceContextCache(int VehicleId, int? CompanyId, string VehiclePlateNumber, string VehicleStatus);

    internal sealed class Handler : IRequestHandler<Command, Result>
    {
        private readonly IGenericRepository<Device, string> _deviceRepo;
        private readonly IGenericRepository<TelemetryEvent, Guid> _telemetryRepo;
        private readonly IDatabase _redis;
        
        public Handler(
            IGenericRepository<Device, string> deviceRepo,
            IGenericRepository<TelemetryEvent, Guid> telemetryRepo,
            IConnectionMultiplexer redisMux)
        {
            _deviceRepo = deviceRepo;
            _telemetryRepo = telemetryRepo;
            _redis = redisMux.GetDatabase();
        }

        public async Task<Result> Handle(Command request, CancellationToken token)
        {
            // A. GET DEVICE CONTEXT (Cache-Aside Pattern)
            // Goal: Avoid DB hits for high-frequency telemetry (10Hz)
            string mappingKey = $"device:{request.DeviceId}:map";
            DeviceContextCache? deviceContext = null;

            // 1. Try Redis
            var cachedMapping = await _redis.StringGetAsync(mappingKey);
            if (!cachedMapping.IsNull)
            {
                deviceContext = JsonSerializer.Deserialize<DeviceContextCache>(cachedMapping.ToString());
            }

            // 2. Fallback to DB (Only happens once per hour per device)
            if (deviceContext == null)
            {
                var deviceDto = await _deviceRepo.GetAll()
                    .AsNoTracking()
                    .Where(d => d.Id == request.DeviceId)
                    .Select(d => new
                    {
                        d.VehicleId,
                        d.Vehicle!.CompanyId,
                        d.Vehicle.PlateNumber,
                        d.Vehicle.Status
                    })
                    .FirstOrDefaultAsync(token);

                if (deviceDto == null || deviceDto.VehicleId == null)
                    return Result.Failure(new Error("Device.Unknown", "Device not linked"));

                // Create Cache Object
                deviceContext = new DeviceContextCache(
                    deviceDto.VehicleId.Value,
                    deviceDto.CompanyId,
                    deviceDto.PlateNumber ?? "Unknown",
                    deviceDto.Status.ToString());


                // Save to Redis (Long TTL because Device->Vehicle mapping rarely changes)
                await _redis.StringSetAsync(mappingKey, JsonSerializer.Serialize(deviceContext), TimeSpan.FromHours(1));
            }

            // B. UPDATE LIVE MAP (Redis Hash)
            string liveKey = $"vehicle:{deviceContext.VehicleId}:live";


            // Since we have the context cached, we can write the FULL hash (including Plate/Status).
            // This prevents "Ghost Vehicles" (missing metadata) if the live key expired.
            var hashEntries = new HashEntry[]
            {
                new HashEntry("Latitude", request.Latitude),
                new HashEntry("Longitude", request.Longitude),
                new HashEntry("SpeedKmh", request.SpeedKmh),
                new HashEntry("LastUpdateUtc", request.Timestamp.ToString("o")),
                new HashEntry("PlateNumber", deviceContext.VehiclePlateNumber), 
                new HashEntry("Status", deviceContext.VehicleStatus)
            };

            await _redis.HashSetAsync(liveKey, hashEntries);
            await _redis.KeyExpireAsync(liveKey, TimeSpan.FromMinutes(5));

            Enum.TryParse<TelemetryEventType>(request.EventType, true, out var eventType);
            var telemetryEvent = new TelemetryEvent
            {
                DeviceId = request.DeviceId,
                VehicleId = deviceContext.VehicleId,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                SpeedKmh = request.SpeedKmh,
                Timestamp = request.Timestamp,
                EventType = eventType
            };

            await _telemetryRepo.AddAsync(telemetryEvent);
            await _telemetryRepo.SaveChangesAsync(token);
            return Result.Success();
        }
    } 
}