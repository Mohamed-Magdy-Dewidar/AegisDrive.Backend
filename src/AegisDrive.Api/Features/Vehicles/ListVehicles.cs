using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Vehicles;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.Pagination;
using AegisDrive.Api.Shared.ResultEndpoint;
using Carter;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AegisDrive.Api.Features.Fleet;

public static class ListVehicles
{

    public record Query(int? CompanyId, VehicleStatus? Status,int Page = 1,int PageSize = 20) : IRequest<Result<PagedResult<ListVehiclesResponse>>>;



    internal sealed class Handler : IRequestHandler<Query, Result<PagedResult<ListVehiclesResponse>>>
    {
        private readonly IGenericRepository<Vehicle , int> _vehicleReposiotry;

        public Handler(IGenericRepository<Vehicle, int> vehicleReposiotry)
        {
            _vehicleReposiotry = vehicleReposiotry;
        }


        public async Task<Result<PagedResult<ListVehiclesResponse>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var query = _vehicleReposiotry.GetAll();

            if (request.CompanyId.HasValue)
            {
                query = query.Where(v => v.CompanyId == request.CompanyId.Value);
            }
            else
            {
                query = query.Where(v => v.CompanyId == null);
            }

            if (request.Status.HasValue)
            {
                query = query.Where(v => v.Status == request.Status);
            }

            var dtoQuery = query
                .OrderByDescending(v => v.CreatedOnUtc)
                .Select(v => new ListVehiclesResponse(
                    v.Id,
                    v.PlateNumber ?? "N/A",
                    v.Model,
                    v.Status.ToString(),
                    v.CurrentDriver != null ? v.CurrentDriver.FullName  : "N/A"
                ));


            var pagedResult = await dtoQuery.ToPagedResultAsync(request.Page, request.PageSize, cancellationToken);
            return Result.Success(pagedResult);
        }
    }

  
 
}