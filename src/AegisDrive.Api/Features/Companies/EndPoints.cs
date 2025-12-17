using Carter;
using MediatR;

namespace AegisDrive.Api.Features.Companies;

public class EndPoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var companiesGroup = app.MapGroup("api/v1/fleet/companies")
                            .WithTags("Companies");

        //GET api/v1/fleet/companies
        // List available companies for registration
        companiesGroup.MapGet("/", async (ISender sender) =>
        {
            var result = await sender.Send(new ListCompanies.Query());
            return Results.Ok(result.Value);
        })
        .AllowAnonymous()         
        .WithSummary("List available companies for registration");

    }
}
