using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.RealTime;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Features.Devices; // Needed for GetDeviceById & UpdateDeviceHeartBeat
using AegisDrive.Api.Hubs;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

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
            RuleFor(x => x.EventType).NotEmpty();
        }
    }

    public record Command(string DeviceId, double Latitude, double Longitude, double SpeedKmh, string EventType, DateTime Timestamp) : ICommand<Result>;

    internal sealed class Handler : IRequestHandler<Command, Result>
    {
        private readonly IGenericRepository<TelemetryEvent, Guid> _telemetryRepo;
        private readonly IDatabase _redis;
        private readonly IHubContext<FleetHub, IFleetClient> _hubContext;
        private readonly ISender _sender;
        private readonly IValidator<Command> _validator; 

        public Handler(
            IGenericRepository<TelemetryEvent, Guid> telemetryRepo,
            IConnectionMultiplexer redisMux,
            IHubContext<FleetHub, IFleetClient> hubContext,
            ISender sender,
            IValidator<Command> validator)
        {
            _telemetryRepo = telemetryRepo;
            _redis = redisMux.GetDatabase();
            _hubContext = hubContext;
            _sender = sender;
            _validator = validator;
        }

        public async Task<Result> Handle(Command request, CancellationToken token)
        {

            var validationResult = await _validator.ValidateAsync(request, token);

            if (!validationResult.IsValid)
                return Result.Failure(new Error("IngestTelemetry.Validation", validationResult?.Errors?.ToString()));

            var contextResult = await _sender.Send(new GetDeviceById.Query(request.DeviceId), token);

            if (contextResult.IsFailure)
            {
                return Result.Failure(contextResult.Error);
            }

            var context = contextResult.Value;

            
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
                await _hubContext.Clients.Group(groupName).ReceiveVehicleUpdate(update);
            }
            // 2. Notify Individual Owner
            else if (!string.IsNullOrEmpty(context.OwnerUserId))
            {
                var groupName = $"User_{context.OwnerUserId}".ToLower();
                await _hubContext.Clients.Group(groupName).ReceiveVehicleUpdate(update);
            }


            await _sender.Send(new UpdateDeviceHeartBeat.Command(request.DeviceId), token);


            return Result.Success();
        }
    }
}