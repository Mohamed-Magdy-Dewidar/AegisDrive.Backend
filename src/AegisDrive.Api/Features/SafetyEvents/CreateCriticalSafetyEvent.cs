using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.SafetyEventsDto;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Entities.Enums.Driver;
using AegisDrive.Api.Shared;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using FluentValidation;
using MediatR;

namespace AegisDrive.Api.Features.SafetyEvents;

public  static class CreateCriticalSafetyEvent
{
    public record Command(
        Guid EventId,
        string? Message,
        double? EarValue,
        double? MarValue,
        double? HeadYaw,
        DriverState DriverState,
        AlertLevel AlertLevel,
        string? S3DriverImagePath,
        string? S3RoadImagePath,
        bool RoadHasHazard,
        int RoadVehicleCount,
        int RoadPedestrianCount,
        double? RoadClosestDistance,
        DateTime Timestamp,
        double Latitude,
        double Longitude,
        double Speed,
        string? DeviceId,
        int? VehicleId,
        int? DriverId,
        int? CompanyId,
        Guid? TripId ) : ICommand<Result<CreatedCriticalSafetyEventResponse>>;





    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.EventId).NotEmpty();
            RuleFor(x => x.Timestamp).NotEmpty();
            RuleFor(x => x.AlertLevel).IsInEnum();
            RuleFor(x => x.DriverState).IsInEnum();
        }
    }






    
    internal sealed record Handler : IRequestHandler<Command, Result<CreatedCriticalSafetyEventResponse>>
    {
        private readonly IGenericRepository<SafetyEvent, Guid> _safetyRepository;
        private readonly IFileStorageService _fileStorageService;
        private readonly IValidator<Command> _Validator;

        


        public Handler(IGenericRepository<SafetyEvent, Guid> safetyRepository , IFileStorageService fileStorageService , IValidator<Command> Validator)
        {
            _safetyRepository = safetyRepository;
            _fileStorageService = fileStorageService;
            _Validator = Validator;
        }

        public async Task<Result<CreatedCriticalSafetyEventResponse>> Handle(Command request, CancellationToken cancellationToken)
        {

            var validationResult = await _Validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
                return Result.Failure<CreatedCriticalSafetyEventResponse>(new Error("CreateCriticalSafetyEvent.Validation",validationResult.ToString()));


            // A. Idempotency Check (Prevent duplicate logs if Pi retries)
            if (await _safetyRepository.AnyAsync(e => e.Id == request.EventId, cancellationToken))
            {
                return Result.Success(new CreatedCriticalSafetyEventResponse(request.EventId.ToString(),  request.S3DriverImagePath , request.S3RoadImagePath )
                {
                    Message = "Event already exists(Idempotent)."
                });
            }
       
            // B. Map Command to Entity
            var safetyEvent = new SafetyEvent
            {
                Id = request.EventId,
                Timestamp = request.Timestamp,
                Message = request.Message,
                 
                
                // Foreign Keys
                DeviceId = request.DeviceId,
                VehicleId = request.VehicleId,
                DriverId = request.DriverId,
                TripId = request.TripId,

                // Enums
                DriverState = request.DriverState,
                AlertLevel = request.AlertLevel,

                // Metrics
                EarValue = request.EarValue,
                MarValue = request.MarValue,
                HeadYaw = request.HeadYaw,

                // Evidence
                S3DriverImagePath = request.S3DriverImagePath,
                S3RoadImagePath = request.S3RoadImagePath,

                // Location
                Latitude = request.Latitude,
                Longitude = request.Longitude,

                // Speed
                Speed = request.Speed,


                // Road Context
                RoadHasHazard = request.RoadHasHazard,
                RoadVehicleCount = request.RoadVehicleCount,
                RoadPedestrianCount = request.RoadPedestrianCount,
                RoadClosestDistance = request.RoadClosestDistance
            };

            int driverIdVal = request.DriverId ?? 0;
            int? companyIdVal = request.CompanyId;

            string baseFolder = FilePaths.GetEventPath(companyIdVal, driverIdVal, request.Timestamp);

            // 1. Move Driver Image
            if (!string.IsNullOrEmpty(request.S3DriverImagePath))
            {
                // Define new key: fleets/.../events/.../driver_image.jpg
                string newKey = $"{baseFolder}/driver_{DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")}.jpg";
                await _fileStorageService.MoveFileAsync(request.S3DriverImagePath, newKey, cancellationToken);
                safetyEvent.S3DriverImagePath = newKey;
            }

            // 2. Move Road Image
            if (!string.IsNullOrEmpty(request.S3RoadImagePath))
            {
                string newKey = $"{baseFolder}/road_{DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")}.jpg";
                await _fileStorageService.MoveFileAsync(request.S3RoadImagePath, newKey, cancellationToken);
                safetyEvent.S3RoadImagePath = newKey;
            }



            await _safetyRepository.AddAsync(safetyEvent);

            return Result.Success(new CreatedCriticalSafetyEventResponse(
                safetyEvent.Id.ToString(),
                safetyEvent.S3DriverImagePath, 
                safetyEvent.S3RoadImagePath    
            ));

        }
    }
}
