using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.SafetyEventsDto; 
using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Entities.Enums.Driver;
using AegisDrive.Api.Shared;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using Amazon.S3; 
using FluentValidation;
using MediatR;
using SafetyEventEntity = AegisDrive.Api.Entities.SafetyEvent;

namespace AegisDrive.Api.Features.SafetyEvents;

public static class CreateSafetyEvent
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
        string? DeviceId,
        int? VehicleId,
        int? DriverId,
        int? CompanyId
    ) : ICommand<Result<CreatedSafetyEventResponse>>;

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

    internal sealed class Handler : IRequestHandler<Command, Result<CreatedSafetyEventResponse>>
    {
        private readonly IGenericRepository<SafetyEventEntity, Guid> _safetyRepository;
        private readonly IFileStorageService _fileStorageService;
        private readonly IValidator<Command> _validator;

        public Handler(
            IGenericRepository<SafetyEventEntity, Guid> safetyRepository,
            IFileStorageService fileStorageService,
            IValidator<Command> validator)
        {
            _safetyRepository = safetyRepository;
            _fileStorageService = fileStorageService;
            _validator = validator;
        }

        public async Task<Result<CreatedSafetyEventResponse>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await _validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
                return Result.Failure<CreatedSafetyEventResponse>(
                    new Error("SafetyEvent.Validation", validationResult.ToString()));

            if (await _safetyRepository.AnyAsync(e => e.Id == request.EventId, cancellationToken))
            {
                // Retrieve existing event to return its paths if needed, or just return success
                var existing = await _safetyRepository.GetByIdAsync(request.EventId);
                return Result.Success(new CreatedSafetyEventResponse(request.EventId.ToString())
                {
                    Message = "Event already exists (Idempotent)."
                });
            }

            var safetyEvent = new SafetyEventEntity
            {
                Id = request.EventId,
                Timestamp = request.Timestamp,
                Message = request.Message,

                DeviceId = request.DeviceId,
                VehicleId = request.VehicleId,
                DriverId = request.DriverId,

                DriverState = request.DriverState,
                AlertLevel = request.AlertLevel,

                EarValue = request.EarValue,
                MarValue = request.MarValue,
                HeadYaw = request.HeadYaw,

                S3DriverImagePath = request.S3DriverImagePath,
                S3RoadImagePath = request.S3RoadImagePath,

                RoadHasHazard = request.RoadHasHazard,
                RoadVehicleCount = request.RoadVehicleCount,
                RoadPedestrianCount = request.RoadPedestrianCount,
                RoadClosestDistance = request.RoadClosestDistance
            };

            // D. File Management (Move from Inbox -> Organized Folder)
            // Use safe defaults for path generation
            int driverIdVal = request.DriverId ?? 0;
            int? companyIdVal = request.CompanyId;
            string baseFolder = FilePaths.GetEventPath(companyIdVal, driverIdVal, request.Timestamp);

            // 1. Move Driver Image
            if (!string.IsNullOrEmpty(request.S3DriverImagePath))
            {
                try
                {
                    string newKey = $"{baseFolder}/driver_{Guid.NewGuid()}.jpg";
                    await _fileStorageService.MoveFileAsync(request.S3DriverImagePath, newKey, cancellationToken);
                    safetyEvent.S3DriverImagePath = newKey; // Update to new path
                }
                catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey")
                {
                    // Log internally or just keep the old path/null
                    // If file is missing, we still want to save the event data!
                    // safetyEvent.S3DriverImagePath = null; // Option: Clear it if invalid
                }
            }

            // 2. Move Road Image
            if (!string.IsNullOrEmpty(request.S3RoadImagePath))
            {
                try
                {
                    string newKey = $"{baseFolder}/road_{Guid.NewGuid()}.jpg";
                    await _fileStorageService.MoveFileAsync(request.S3RoadImagePath, newKey, cancellationToken);
                    safetyEvent.S3RoadImagePath = newKey;
                }
                catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey")
                {
                    // Ignore missing file
                }
            }

            await _safetyRepository.AddAsync(safetyEvent);            
            await _safetyRepository.SaveChangesAsync(cancellationToken);



            return Result.Success(new CreatedSafetyEventResponse(
                safetyEvent.Id.ToString()
            ));
        }
    }
}