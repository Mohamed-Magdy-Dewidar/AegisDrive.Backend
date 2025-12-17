using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Drivers;
using AegisDrive.Api.DataBase;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.ResultEndpoint;
using Carter;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AegisDrive.Api.Features.Companies;

public static class ListCompanies
{

    public record ListCompaniesCompanyDto(int Id, string Name);

    public record Query() : IRequest<Result<IEnumerable<ListCompaniesCompanyDto>>>;

    internal sealed class Handler : IRequestHandler<Query, Result<IEnumerable<ListCompaniesCompanyDto>>>
    {

        private readonly IGenericRepository<Company , int> _companyRepo;
        public Handler(IGenericRepository<Company, int> companyRepo)
        {
            _companyRepo = companyRepo;
        }
        public async Task<Result<IEnumerable<ListCompaniesCompanyDto>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var companiesDto = await _companyRepo
            .GetAll(trackChanges: false)
            .Select(c => new ListCompaniesCompanyDto(c.Id, c.Name)) 
            .ToListAsync(cancellationToken);

            if(companiesDto == null)
                return Result.Failure<IEnumerable<ListCompaniesCompanyDto>>(new Error("Companies.NotFound", "No companies found."));

            return Result.Success<IEnumerable<ListCompaniesCompanyDto>>(companiesDto);
        
        }
    }
    
}