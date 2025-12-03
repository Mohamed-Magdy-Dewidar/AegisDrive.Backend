using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Drivers;
using AegisDrive.Api.DataBase;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Shared;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using FluentValidation;
using MediatR;

namespace AegisDrive.Api.Features.Drivers;

public static class UpdateDriver
{
    public record Command(int DriverId,string? FullName,string? PhoneNumber,string? Email,int? CompanyId) : ICommand<Result<UpdateDriverResponse>>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.DriverId).NotEmpty();

            // Validations only apply IF the field is being updated (not null)
            RuleFor(x => x.FullName)
                .NotEmpty().MaximumLength(100)
                .When(x => !string.IsNullOrEmpty(x.FullName));

            RuleFor(x => x.Email)
                .NotEmpty().EmailAddress()
                .When(x => !string.IsNullOrEmpty(x.Email));

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().MinimumLength(10)
                .When(x => !string.IsNullOrEmpty(x.PhoneNumber));
        }
    }

    internal sealed class Handler : IRequestHandler<Command, Result<UpdateDriverResponse>>
    {
        private readonly IGenericRepository<Driver, int> _driversRepository;
        private readonly IGenericRepository<Company, int> _companiesRepository;
        private readonly IValidator<Command> _validator;
        

        public Handler(IGenericRepository<Driver, int> driversRepository,IGenericRepository<Company, int> companiesRepository,IValidator<Command> validator)
        {
            _driversRepository = driversRepository;
            _companiesRepository = companiesRepository;
            _validator = validator;
        }

        public async Task<Result<UpdateDriverResponse>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await _validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
                return Result.Failure<UpdateDriverResponse>(new Error("Driver.Validation", validationResult.ToString()));

            var driver = await _driversRepository.GetByIdAsync(request.DriverId);
            if (driver is null)
                return Result.Failure<UpdateDriverResponse>(new Error("Driver.NotFound", $"Driver with ID {request.DriverId} does not exist."));

            // C. Check for Duplicates (Only if Email/Phone changed)
            if (request.Email != null || request.PhoneNumber != null)
            {
                bool isDuplicate = await _driversRepository.AnyAsync(
                    d => d.Id != request.DriverId &&
                        ((request.Email != null && d.Email == request.Email) ||
                         (request.PhoneNumber != null && d.PhoneNumber == request.PhoneNumber)),
                    cancellationToken);

                if (isDuplicate)
                    return Result.Failure<UpdateDriverResponse>(new Error("Driver.Conflict", "Another driver with this email or phone number already exists."));
            }


            if (request.CompanyId.HasValue)
            {
                bool companyExists = await _companiesRepository.AnyAsync(c => c.Id == request.CompanyId, cancellationToken);
                if (!companyExists)
                    return Result.Failure<UpdateDriverResponse>(new Error("Company.NotFound", $"Company with ID {request.CompanyId} was not found."));
            }

            var updatedProperties = new List<string>();

            // Use helper method to handle logic cleanly
            await UpdateDriverProperties(updatedProperties, driver, request, cancellationToken);

           
            if (updatedProperties.Count > 0)
            {
                _driversRepository.SaveInclude(driver, updatedProperties.ToArray());
                // Note: TransactionalMiddleware handles final commit
            }

            return Result.Success(new UpdateDriverResponse(driver.Id, "Driver updated successfully."));
        }


        private async Task UpdateDriverProperties(List<string> updatedProperties, Driver driver, Command request, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(request.FullName))
            {
                driver.FullName = request.FullName;
                updatedProperties.Add(nameof(Driver.FullName));
            }

            if (!string.IsNullOrEmpty(request.Email))
            {
                driver.Email = request.Email;
                updatedProperties.Add(nameof(Driver.Email));
            }

            if (!string.IsNullOrEmpty(request.PhoneNumber))
            {
                driver.PhoneNumber = request.PhoneNumber;
                updatedProperties.Add(nameof(Driver.PhoneNumber));
            }

            if (request.CompanyId.HasValue)
            {
                driver.CompanyId = request.CompanyId;
                updatedProperties.Add(nameof(Driver.CompanyId));
            }

            //if (request.Image != null)
            //{
            //    // 1. Determine Path (Use updated CompanyId if it changed, otherwise existing)
            //    int? targetCompanyId = request.CompanyId.HasValue ? request.CompanyId : driver.CompanyId;

            //    if (!string.IsNullOrEmpty(driver.PictureUrl))
            //        await _fileStorageService.DeleteAsync(driver.PictureUrl);
               
            //    string folderPath = FilePaths.GetDriverProfilePath(targetCompanyId, driver.Id);

            //    // 2. Upload
            //    string s3Key = await _fileStorageService.UploadAsync(request.Image, folderPath, cancellationToken);

            //    // 3. Update Entity
            //    driver.PictureUrl = s3Key;
            //    updatedProperties.Add(nameof(Driver.PictureUrl));
            //}
        }
    }
}