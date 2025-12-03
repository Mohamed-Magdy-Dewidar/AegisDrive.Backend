using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Vehicles;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Shared;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using Carter;
using FluentValidation;
using MediatR;

namespace AegisDrive.Api.Features.Fleet;

public static class RegisterVehicle
{

    public record Command(string PlateNumber, string? Model,int? CompanyId) : ICommand<Result<RegisterVehicleResponse>>;

    
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.PlateNumber)
                .NotEmpty().WithMessage("Plate number is required.")
                .MaximumLength(20);

            RuleFor(x => x.Model)
                .MaximumLength(50);

            When(x => x.CompanyId.HasValue, () =>
            {
                RuleFor(x => x.CompanyId).GreaterThan(0);
            });
        }
    }

   
    internal sealed class Handler : IRequestHandler<Command, Result<RegisterVehicleResponse>>
    {
        private readonly IGenericRepository<Vehicle, int> _vehicleRepository;
        private readonly IGenericRepository<Company, int> _companyRepository;
        private readonly IValidator<Command> _validator;

        public Handler(IGenericRepository<Vehicle, int> vehicleRepository,IGenericRepository<Company, int> companyRepository,IValidator<Command> validator)
        {
            _vehicleRepository = vehicleRepository;
            _companyRepository = companyRepository;
            _validator = validator;
        }

        public async Task<Result<RegisterVehicleResponse>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await _validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
                return Result.Failure<RegisterVehicleResponse>(new Error("Vehicle.Validation", validationResult.ToString()));
            

            bool isDuplicate = await _vehicleRepository.AnyAsync(v => v.PlateNumber == request.PlateNumber , cancellationToken);

            if (isDuplicate)
                return Result.Failure<RegisterVehicleResponse>(new Error("Vehicle.Conflict", "A vehicle with this Plate Number or VIN already exists."));

            if (request.CompanyId.HasValue)
            {
                bool companyExists = await _companyRepository.AnyAsync(c => c.Id == request.CompanyId, cancellationToken);
                if (!companyExists)
                    return Result.Failure<RegisterVehicleResponse>(new Error("Company.NotFound", $"Company with ID {request.CompanyId} was not found."));
            }

            var vehicle = new Vehicle
            {
                PlateNumber = request.PlateNumber,
                Model = request.Model,
                CompanyId = request.CompanyId,
                Status = VehicleStatus.Active, // Default to Active
                CurrentDriverId = null // No driver assigned initially
            };

            await _vehicleRepository.AddAsync(vehicle);
            await _vehicleRepository.SaveChangesAsync();

        
            return Result.Success(new RegisterVehicleResponse(vehicle.Id));
        }
    }

   

}