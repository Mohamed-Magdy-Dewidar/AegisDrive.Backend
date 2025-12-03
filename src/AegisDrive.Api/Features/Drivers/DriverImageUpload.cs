using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Drivers;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Shared;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using FluentValidation;
using MediatR;

namespace AegisDrive.Api.Features.Drivers;


public static class DriverImageUpload
{
    public record Command(int DriverId, IFormFile Image) : ICommand<Result<string>>;



    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {

            RuleFor(x => x.Image).NotNull().NotEmpty();
            RuleFor(x => x.Image!.Length).LessThan(5 * 1024 * 1024).WithMessage("Image must be under 5MB.");
            RuleFor(x => x.Image!.ContentType).Must(x => x.Equals("image/jpeg") || x.Equals("image/png"))
                    .WithMessage("Only JPG and PNG files are allowed.");
        }
    }

    public class Handler : IRequestHandler<Command, Result<string>>
    {
        private readonly IGenericRepository<Driver, int> _driversRepository;
        private readonly IFileStorageService _fileStorageService;
        private readonly IValidator<Command> _Validator;
    
        public Handler(IGenericRepository<Driver, int> driversRepository, IFileStorageService fileStorageService , IValidator<Command> Validator)
        {
            _driversRepository = driversRepository;
            _Validator = Validator;
            _fileStorageService = fileStorageService;
        }

        public async Task<Result<string>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await _Validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
                return Result.Failure<string>(new Error("Driver.Validation", validationResult.ToString()));

            var driver = await _driversRepository.GetByIdAsync(request.DriverId);
            if (driver == null)
                return Result.Failure<string>(new Error("Driver.NotFound", $"Driver with ID {request.DriverId} does not exist."));
            
            if (!string.IsNullOrEmpty(driver.PictureUrl))
                await _fileStorageService.DeleteAsync(driver.PictureUrl);

            string folderPath = FilePaths.GetDriverProfilePath(driver.CompanyId, driver.Id);
            string s3Key = await _fileStorageService.UploadAsync(request.Image, folderPath, cancellationToken);

            driver.PictureUrl = s3Key;
            _driversRepository.SaveInclude(driver, nameof(driver.PictureUrl));


            return Result.Success(s3Key);
        }
    }
}


