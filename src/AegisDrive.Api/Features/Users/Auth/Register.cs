using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Users;
using AegisDrive.Api.DataBase;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Identity;
using AegisDrive.Api.Shared.Auth;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;

using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace AegisDrive.Api.Features.Users.Auth;


public static class Register
{
    

    
    public record Command(
        string Email,
        string Password,
        string FullName,
        string AccountType,
        string? CompanyName
    ) : ICommand<Result<RegisterResponse>>;


    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
            RuleFor(x => x.FullName).NotEmpty();

            RuleFor(x => x.AccountType)
                .Must(x => x == "Individual" || x == "Company")
                .WithMessage("Account Type must be 'Individual' or 'Company'");

            RuleFor(x => x.CompanyName)
                .NotEmpty()
                .When(x => x.AccountType == "Company")
                .WithMessage("Company Name is required for Company accounts.");
        }
    }

    internal sealed class Handler : IRequestHandler<Command, Result<RegisterResponse>>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITokenProvider _tokenProvider;
        private readonly AppDbContext _dbContext;

        public Handler(UserManager<ApplicationUser> userManager,ITokenProvider tokenProvider,AppDbContext dbContext)
        {
            _userManager = userManager;
            _tokenProvider = tokenProvider;
            _dbContext = dbContext;
        }

        public async Task<Result<RegisterResponse>> Handle(Command request, CancellationToken token)
        {
            // 1. Business Validation (No DB writes yet)
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser is not null)
            {
                return Result.Failure<RegisterResponse>(new Error("Auth.Duplicate", "Email is already in use."));
            }

            int? newCompanyId = null;
            string userRole = AuthConstants.Roles.Individual;


            if (!AuthConstants.AccountTypes.AvailableAccountTypes.Contains(request.AccountType))
            {
                return Result.Failure<RegisterResponse>(new Error("Auth.InvalidData", "Account Type must be 'Individual' or 'Company'."));
            }

            // 2. Create Company (If applicable)
            if (request.AccountType == AuthConstants.AccountTypes.Company)
            {

                if(string.IsNullOrEmpty(request.CompanyName))
                {
                    return Result.Failure<RegisterResponse>(new Error("Auth.InvalidData", "Company Name is required for Company accounts."));
                }

                var company = new Company
                {
                    Name = request.CompanyName,
                };

                _dbContext.Companies.Add(company);
                await _dbContext.SaveChangesAsync(token); 

                newCompanyId = company.Id;
                userRole = AuthConstants.Roles.Manager;
            }

            // 3. Create Identity User
            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName,
                CompanyId = newCompanyId
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);

            if (!createResult.Succeeded)
            {                
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                throw new Exception($"Identity Creation Failed: {errors}");
            }

            // 4. Assign Role
            await _userManager.AddToRoleAsync(user, userRole);

            // 5. Generate Token
            var jwt = await _tokenProvider.CreateTokenAsync(user);

            // 6. Return (Middleware will Commit automatically upon success)
            return Result.Success(new RegisterResponse(jwt, user.FullName, userRole));
        }
    }

    
}