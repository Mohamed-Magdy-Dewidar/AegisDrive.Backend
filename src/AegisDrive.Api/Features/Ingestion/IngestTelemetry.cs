using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.RealTime;
using AegisDrive.Api.DataBase;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Hubs;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace AegisDrive.Api.Features.Ingestion;

public static class IngestTelemetry
{
    // 1. Validator
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.DeviceId).NotEmpty();
            RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
            RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
            RuleFor(x => x.EventType).NotEmpty();
        }
    }

    // 2. Command
    public record Command(string DeviceId, double Latitude, double Longitude, double SpeedKmh, string EventType, DateTime Timestamp) : ICommand<Result>;

    // 3. Cache Model (Stores the String ID now)
    private record DeviceContextCache(
        int VehicleId,
        int? CompanyId,
        string? OwnerUserId, // <--- Storing the GUID here
        string PlateNumber,
        string Status
    );

    // 4. Handler
    internal sealed class Handler : IRequestHandler<Command, Result>
    {
        private readonly IGenericRepository<Device, string> _deviceRepo;
        private readonly IGenericRepository<TelemetryEvent, Guid> _telemetryRepo;
        private readonly IDatabase _redis;
        private readonly IHubContext<FleetHub, IFleetClient> _hubContext;

        public Handler(
            IGenericRepository<Device, string> deviceRepo,
            IGenericRepository<TelemetryEvent, Guid> telemetryRepo,
            IConnectionMultiplexer redisMux,
            IHubContext<FleetHub, IFleetClient> hubContext)
        {
            _deviceRepo = deviceRepo;
            _telemetryRepo = telemetryRepo;
            _redis = redisMux.GetDatabase();
            _hubContext = hubContext;
        }

        public async Task<Result> Handle(Command request, CancellationToken token)
        {
            // --- A. GET CONTEXT (Redis Cache) ---
            string mappingKey = $"device:{request.DeviceId}:map";
            DeviceContextCache? context = null;

            var cachedMapping = await _redis.StringGetAsync(mappingKey);
            if (!cachedMapping.IsNull)
            {
                context = JsonSerializer.Deserialize<DeviceContextCache>(cachedMapping.ToString());
            }

            // --- B. FALLBACK TO DB ---
            if (context == null)
            {
                // We fetch the 'OwnerUserId' (String) here
                var deviceDto = await _deviceRepo.GetAll()
                    .AsNoTracking()
                    .Where(d => d.Id == request.DeviceId)
                    .Select(d => new
                    {
                        d.VehicleId,
                        d.Vehicle!.CompanyId,
                        d.Vehicle.OwnerUserId, // <--- NEW COLUMN
                        d.Vehicle.PlateNumber,
                        d.Vehicle.Status
                    })
                    .FirstOrDefaultAsync(token);

                if (deviceDto == null || deviceDto.VehicleId == null)
                    return Result.Failure(new Error("Device.Unknown", "Device not linked"));

                context = new DeviceContextCache(
                    deviceDto.VehicleId.Value,
                    deviceDto.CompanyId,
                    deviceDto.OwnerUserId?.ToLower(),
                    deviceDto.PlateNumber ?? "Unknown",
                    deviceDto.Status.ToString());

                // Cache for 1 hour
                await _redis.StringSetAsync(mappingKey, JsonSerializer.Serialize(context), TimeSpan.FromHours(1));
            }

            // --- C. UPDATE LIVE MAP (Redis Hash) ---
            string liveKey = $"vehicle:{context.VehicleId}:live";
            var hashEntries = new HashEntry[]
            {
                new HashEntry("Latitude", request.Latitude),
                new HashEntry("Longitude", request.Longitude),
                new HashEntry("SpeedKmh", request.SpeedKmh),
                new HashEntry("PlateNumber", context.PlateNumber),
                new HashEntry("Status", context.Status)
            };
            await _redis.HashSetAsync(liveKey, hashEntries);
            await _redis.KeyExpireAsync(liveKey, TimeSpan.FromMinutes(5));

            // --- D. SAVE EVENT TO SQL ---
            Enum.TryParse<TelemetryEventType>(request.EventType, true, out var eventType);
            var telemetryEvent = new TelemetryEvent
            {
                DeviceId = request.DeviceId,
                VehicleId = context.VehicleId,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                SpeedKmh = request.SpeedKmh,
                Timestamp = request.Timestamp,
                EventType = eventType
            };
            await _telemetryRepo.AddAsync(telemetryEvent);
            await _telemetryRepo.SaveChangesAsync(token);

            // --- E. PUSH TO SIGNALR ---
            var update = new VehicleTelemetryUpdate(
                context.VehicleId,
                context.PlateNumber,
                request.Latitude,
                request.Longitude,
                request.SpeedKmh,
                request.EventType.ToString(),
                DateTime.UtcNow
            );


            // 1. Notify Company Managers
            if (context.CompanyId.HasValue)
            {
                var groupName = $"Company_{context.CompanyId}".ToLower();
                await _hubContext.Clients
                    .Group(groupName)
                    .ReceiveVehicleUpdate(update);
            }
            // 2. Notify Individual Owner
            else if (!string.IsNullOrEmpty(context.OwnerUserId))
            {
                // "user_56d5e237-c1bf-417a-a2f2-0a480be93754"
                // correct one -> "user_0ded8209-fe72-4c18-ae10-0fdbbaf727c8"
                var groupName = $"User_{context.OwnerUserId}".ToLower();
                await _hubContext.Clients
                    .Group(groupName)
                    .ReceiveVehicleUpdate(update);
            }

            return Result.Success();
        }
    }    
}