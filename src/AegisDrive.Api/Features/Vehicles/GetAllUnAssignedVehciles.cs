using AegisDrive.Api.Contracts;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Shared.MarkerInterface;
using AegisDrive.Api.Shared.Pagination;
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AegisDrive.Api.Features.Vehicles;

public class GetAllUnAssignedVehciles
{
    // Query remains the same
    public record Query(int? CompanyId, int? DriverId, int Page = 1, int PageSize = 20)
        : IRequest<Result<PagedResult<GetAllUnAssignedVehcilesResponse>>>;

    // Matches your frontend VehicleDetails interface
    public record GetAllUnAssignedVehcilesResponse(
        int Id,
        string PlateNumber,
        string Model,
        string Status = "Available"
    );

    internal sealed class Handler : IRequestHandler<Query, Result<PagedResult<GetAllUnAssignedVehcilesResponse>>>
    {
        private readonly IGenericRepository<Vehicle, int> _vehicleRepository;

        public Handler(IGenericRepository<Vehicle, int> vehicleRepository)
        {
            _vehicleRepository = vehicleRepository;
        }

        public async Task<Result<PagedResult<GetAllUnAssignedVehcilesResponse>>> Handle(Query request, CancellationToken cancellationToken)
        {
            // 1. Start with all vehicles
            var query = _vehicleRepository.GetAll();

            // 2. Filter by Company if provided
            if (request.CompanyId.HasValue)
            {
                query = query.Where(v => v.CompanyId == request.CompanyId.Value);
            }

            // 2. 🔒 SECURITY FIX: If it's a Solo Driver (Individual)
            // Even if the shift ended, they should still see THEIR vehicle in the unassigned list
            if (request.DriverId.HasValue && !request.CompanyId.HasValue)
            {
                // Adjust this logic based on how you link Solo Drivers to Vehicles
                // e.g., v.OwnerId == request.DriverId or similar
                query = query.Where(v => v.CurrentDriverId == request.DriverId.Value);
            }

            // 3. Logic: Find vehicles that do NOT have an active assignment
            // An active assignment is one where UnassignedAt is NULL
            query = query.Where(v => !v.VehicleAssignments.Any(va => va.UnassignedAt == null));

            // 4. Project to the Response DTO
            var projection = query.Select(v => new GetAllUnAssignedVehcilesResponse(
                v.Id,
                v.PlateNumber,
                v.Model ?? "Unknown",
                "Available"
            ));

            // 5. Apply Pagination (Assuming your repository or a helper handles PagedResult)
            var pagedResult = await projection.ToPagedResultAsync(request.Page, request.PageSize, cancellationToken);

            return Result.Success(pagedResult);
        }
    }
}