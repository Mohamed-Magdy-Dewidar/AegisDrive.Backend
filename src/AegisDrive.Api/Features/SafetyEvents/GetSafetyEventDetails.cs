using AegisDrive.Api.Contracts;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Entities.Enums.Driver;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace AegisDrive.Api.Features.SafetyEvents;

public static class GetSafetyEventDetails
{
    public record Query(Guid Id) : IRequest<Result<GetSafetyEventDetailsResponse>>;

    public record GetSafetyEventDetailsResponse(
        Guid Id,
        string? Message,
        double? EarValue,
        double? MarValue,
        double? HeadYaw,
        bool RoadHasHazard,
        int RoadVehicleCount,
        int RoadPedestrianCount,
        double? RoadClosestDistance,
        DateTime Timestamp,
        string? DeviceId,
        int? VehicleId,
        string? VehiclePlate, 
        int? DriverId,
        string? DriverFullName, 
        AlertLevel AlertLevel,
        DriverState DriverState,
        double Latitude,
        double Longitude, 
        double Speed)    
    {
        public string? S3DriverImagePath { get; set; } = string.Empty;
        public string? S3RoadImagePath { get; set; } = string.Empty;
    }

    // 3. Handler
    internal sealed class Handler : IRequestHandler<Query, Result<GetSafetyEventDetailsResponse>>
    {
        private readonly IGenericRepository<SafetyEvent, Guid> _safetyRepository;
        private readonly IFileStorageService _fileStorageService;
        private readonly IDatabase _redis;

        public Handler(
            IGenericRepository<SafetyEvent, Guid> safetyRepository,
            IFileStorageService fileStorageService,
            IConnectionMultiplexer redisMux)
        {
            _safetyRepository = safetyRepository;
            _fileStorageService = fileStorageService;
            _redis = redisMux.GetDatabase();
        }

        public async Task<Result<GetSafetyEventDetailsResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            string cacheKey = $"safety_event:{request.Id}:details";
            GetSafetyEventDetailsResponse? eventDto = null;

            var cachedData = await _redis.StringGetAsync(cacheKey);
            if (!cachedData.IsNull)
            {
                eventDto = JsonSerializer.Deserialize<GetSafetyEventDetailsResponse>(cachedData.ToString());
            }

            if (eventDto is null)
            {
                eventDto = await _safetyRepository.GetAll()
                    .AsNoTracking()
                    .Where(e => e.Id == request.Id)
                    .Select(e => new GetSafetyEventDetailsResponse(
                        e.Id,
                        e.Message,
                        e.EarValue,
                        e.MarValue,
                        e.HeadYaw,
                        e.RoadHasHazard,
                        e.RoadVehicleCount,
                        e.RoadPedestrianCount,
                        e.RoadClosestDistance,
                        e.Timestamp,
                        e.DeviceId,
                        e.VehicleId,
                        e.Vehicle != null ? e.Vehicle.PlateNumber : null, 
                        e.DriverId,
                        e.Driver != null ? e.Driver.FullName : null,      
                        e.AlertLevel,
                        e.DriverState,
                        e.Latitude,
                        e.Longitude,                        
                        e.Speed
                    )
                    {
                        S3DriverImagePath = e.S3DriverImagePath,
                        S3RoadImagePath = e.S3RoadImagePath,
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (eventDto is null)
                {
                    return Result.Failure<GetSafetyEventDetailsResponse>(
                        new Error("SafetyEvent.NotFound", $"Event with ID {request.Id} was not found."));
                }

                // C. Save to Cache (Cache RAW keys, expire in 1 hour)
                var serializedData = JsonSerializer.Serialize(eventDto);
                await _redis.StringSetAsync(cacheKey, serializedData, TimeSpan.FromHours(1));
            }


            if (!string.IsNullOrEmpty(eventDto.S3DriverImagePath))
            {
                eventDto.S3DriverImagePath = _fileStorageService.GetPresignedUrl(eventDto.S3DriverImagePath);
                
            }

            if (!string.IsNullOrEmpty(eventDto.S3RoadImagePath))
            {
                eventDto.S3RoadImagePath = _fileStorageService.GetPresignedUrl(eventDto.S3RoadImagePath);
            }

            return Result.Success(eventDto);
        }
    }

  
}