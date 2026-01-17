// src/AegisDrive.Api/Features/Vehicles/GetAssignedVehicles.cs

using AegisDrive.Api.Contracts;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Shared.Pagination;
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AegisDrive.Api.Features.Vehicles;

public class GetAssignedVehicles
{
    public record Query(int? CompanyId, int? DriverId ,  int Page = 1, int PageSize = 20)
        : IRequest<Result<PagedResult<GetAssignedVehiclesResponse>>>;

    public record GetAssignedVehiclesResponse(
        int Id,
        string PlateNumber,
        string Model,
        string CurrentDriverName,
        int CurrentDriverId,
        DateTime ShiftStartedAt
    );

    internal sealed class Handler : IRequestHandler<Query, Result<PagedResult<GetAssignedVehiclesResponse>>>
    {
        private readonly IGenericRepository<Vehicle, int> _vehicleRepository;

        public Handler(IGenericRepository<Vehicle, int> vehicleRepository)
        {
            _vehicleRepository = vehicleRepository;
        }

        public async Task<Result<PagedResult<GetAssignedVehiclesResponse>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var query = _vehicleRepository.GetAll()
                .Include(v => v.VehicleAssignments)
                .ThenInclude(a => a.Driver)
                .Where(v => v.VehicleAssignments.Any(va => va.UnassignedAt == null));

            if (request.CompanyId.HasValue)
            {
                query = query.Where(v => v.CompanyId == request.CompanyId.Value);
          
            }
            if (request.DriverId.HasValue)
            {
                query = query.Where(v => v.CurrentDriverId == request.DriverId.Value);
            }

            var projection = query.Select(v => v.VehicleAssignments
                .Where(va => va.UnassignedAt == null)
                .Select(va => new GetAssignedVehiclesResponse(
                    v.Id,
                    v.PlateNumber,
                    v.Model ?? "N/A",
                    va.Driver.FullName,
                    va.DriverId,
                    va.AssignedAt
                )).FirstOrDefault());

            var pagedResult = await projection.ToPagedResultAsync(request.Page, request.PageSize, cancellationToken);

            return Result<PagedResult<GetAssignedVehiclesResponse>>.Success(pagedResult);
        }
    }
}