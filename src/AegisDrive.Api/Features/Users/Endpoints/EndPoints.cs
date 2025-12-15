using AegisDrive.Api.Contracts.Users;
using Carter;
using MediatR;

namespace AegisDrive.Api.Features.Users.Endpoints;

public class EndPoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("api/v1/auth/register", async (RegisterRequest req, ISender sender) =>
        {
            var command = new Register.Command(
                req.Email, req.Password, req.FullName, req.AccountType, req.CompanyName
            );
            
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
                        
        })
        .WithTags("Auth")
        .AllowAnonymous();


        app.MapPost("api/v1/auth/login", async (LoginRequest req, ISender sender) =>
        {
            var command = new Login.Command(req.Email, req.Password);

            var result = await sender.Send(command);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(result.Error);
        })
          .WithTags("Auth")
          .AllowAnonymous();





    }
}
