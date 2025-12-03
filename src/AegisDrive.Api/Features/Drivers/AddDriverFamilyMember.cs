using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Drivers;
using AegisDrive.Api.DataBase;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using FluentValidation;
using MediatR;


namespace AegisDrive.Api.Features.Drivers;

public static class AddDriverFamilyMember
{
    public record Command(string FullName, string PhoneNumber, string Email, string? Relationship, bool NotifyOnCritical, int DriverId) :
        ICommand<Result<AddFamilyMemberResponse>>;



    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.PhoneNumber).NotEmpty().MinimumLength(10);
        }
    }




    internal sealed class Handler : IRequestHandler<Command, Result<AddFamilyMemberResponse>>
    {
        private readonly IGenericRepository<FamilyMember, int> _familyMembersRepository;
        private readonly IGenericRepository<Driver, int> _driversRepository;
        private readonly IValidator<Command> _Validator;

        public Handler(IGenericRepository<Driver, int> driversRepository, IGenericRepository<FamilyMember, int> familyMembersRepository, IValidator<Command> Validator)
        {
            _driversRepository = driversRepository;
            _familyMembersRepository  = familyMembersRepository;
            _Validator = Validator;
        }
        public async Task<Result<AddFamilyMemberResponse>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await _Validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
                return Result.Failure<AddFamilyMemberResponse>(new Error("ValidationError", validationResult.ToString()) );


            bool doesDriverExist = await _driversRepository
                .AnyAsync(driver => driver.Id == request.DriverId, cancellationToken);

            if (!doesDriverExist)
                return Result.Failure<AddFamilyMemberResponse>(new Error("NotFound", "Driver not found.") );
            
            
            var member = new FamilyMember(request.DriverId, request.FullName, request.PhoneNumber, request.Email, request.Relationship, request.NotifyOnCritical);



            await _familyMembersRepository.AddAsync(member);
            await _familyMembersRepository.SaveChangesAsync();

            return Result.Success<AddFamilyMemberResponse>(new AddFamilyMemberResponse(member.Id, "Family member created successfully."));

        }
    }




}
