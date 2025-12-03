using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Drivers;
using AegisDrive.Api.DataBase;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Shared;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AegisDrive.Api.Features.Drivers;
public static class RegisterDriver
{


    public record Command(string FullName, string PhoneNumber, string Email , IFormFile Image , int? CompanyId) : ICommand<Result<RegisterDriverResponse>>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.PhoneNumber).NotEmpty().MinimumLength(10);
            When(x => x.CompanyId.HasValue, () =>
            {
                RuleFor(x => x.CompanyId).GreaterThan(0)
                    .WithMessage("Company ID must be greater than 0.");
            });
            RuleFor(x => x.Image).NotNull().NotEmpty();
            When(x => x.Image != null, () => {
                RuleFor(x => x.Image!.Length).LessThan(5 * 1024 * 1024).WithMessage("Image must be under 5MB.");
                RuleFor(x => x.Image!.ContentType).Must(x => x.Equals("image/jpeg") || x.Equals("image/png"))
                    .WithMessage("Only JPG and PNG files are allowed.");
            });
        }
    }

    internal sealed class Handler : IRequestHandler<Command, Result<RegisterDriverResponse>>
    {
        private readonly IGenericRepository<Driver, int> _driversRepository;
        private readonly IGenericRepository<Company, int> _companiesRepository;
        private readonly IValidator<Command> _Validator;
        private readonly IFileStorageService _fileStorageService;
        
        public Handler(IGenericRepository<Driver, int> driversRepository , IGenericRepository<Company, int> companiesRepository, IFileStorageService fileStorageService ,  IValidator<Command> Validator)
        {
            _driversRepository = driversRepository;
            _companiesRepository = companiesRepository;
            _Validator = Validator;
            _fileStorageService = fileStorageService;
        }



        public async Task<Result<RegisterDriverResponse>> Handle(Command request, CancellationToken cancellationToken)        
        {
            var validationResult = await _Validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Result.Failure<RegisterDriverResponse>(new Error("Driver.Validation", validationResult.ToString()));
            }

            bool isDuplicate = await _driversRepository
                .AnyAsync(d => d.Email == request.Email || d.PhoneNumber == request.PhoneNumber, cancellationToken);


            if (isDuplicate)
                return Result.Failure<RegisterDriverResponse>(new Error("Driver.Conflict", "A driver with this email or phone number already exists."));



            if (request.CompanyId.HasValue)
            {
                bool companyExists = await _companiesRepository.AnyAsync( c => c.Id == request.CompanyId, cancellationToken);
                if (!companyExists)
                    return Result.Failure<RegisterDriverResponse>(new Error("Company.NotFound", $"Company with ID {request.CompanyId} was not found."));                
            }

           
            var driver = new Driver(request.FullName, request.Email, request.PhoneNumber  , PictureUrl: "" , request.CompanyId);            

            await _driversRepository.AddAsync(driver);
            await _driversRepository.SaveChangesAsync();


            if (request.Image != null)
            {   
                string folderPath = FilePaths.GetDriverProfilePath(driver.CompanyId, driver.Id);                
                string s3Key = await _fileStorageService.UploadAsync(request.Image, folderPath, cancellationToken);
                driver.PictureUrl = s3Key;
                _driversRepository.SaveInclude(driver , nameof(driver.PictureUrl));
            }



            return Result.Success< RegisterDriverResponse>(new RegisterDriverResponse(driver.Id));

        }


    }


}