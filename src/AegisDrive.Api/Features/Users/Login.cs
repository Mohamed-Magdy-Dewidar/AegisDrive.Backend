using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Users;
using AegisDrive.Api.Entities.Identity;
using AegisDrive.Api.Shared.Auth;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace AegisDrive.Api.Features.Users;

public static class Login
{
    // 1. Command
    public record Command(string Email, string Password) : ICommand<Result<LoginResponse>>;

    // 2. Validator
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Password).NotEmpty();
        }
    }

    // 3. Handler
    internal sealed class Handler : IRequestHandler<Command, Result<LoginResponse>>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITokenProvider _tokenProvider;

        public Handler(
            UserManager<ApplicationUser> userManager,
            ITokenProvider tokenProvider)
        {
            _userManager = userManager;
            _tokenProvider = tokenProvider;
        }

        public async Task<Result<LoginResponse>> Handle(Command request, CancellationToken token)
        {
            // A. Find User
            var user = await _userManager.FindByEmailAsync(request.Email);

            // Security: We intentionally do not reveal if the user was found or not.
            if (user is null)
            {
                return Result.Failure<LoginResponse>(new Error("Auth.Failed", "Invalid email or password"));
            }

            // B. Validate Password (Using UserManager directly)
            var isPasswordCorrect = await _userManager.CheckPasswordAsync(user, request.Password);

            if (!isPasswordCorrect)
            {
                // Optional: You could add _userManager.AccessFailedAsync(user) here if you want to implement lockouts manually.
                return Result.Failure<LoginResponse>(new Error("Auth.Failed", "Invalid email or password"));
            }

            // C. Generate Token
            var jwt = await _tokenProvider.CreateTokenAsync(user);

            // D. Determine Role (UI Helper)
            string role = user.CompanyId.HasValue
                ? AuthConstants.Roles.Manager
                : AuthConstants.Roles.Individual;

            return Result.Success(new LoginResponse(jwt, user.FullName, role, user.CompanyId));
        }
    }
}