using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.SafetyEventsDto;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Shared.Pagination; 
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;

namespace AegisDrive.Api.Features.SafetyEvents;

public static class GetSafetyEvents
{
    public record Query(
        int? DriverId,
        int? CompanyId,
        DateTime? FromDate,
        DateTime? ToDate,
        AlertLevel? MinLevel,
        int Page = 1,
        int PageSize = 10
    ) : IRequest<Result<PagedResult<GetSafetyEventsResponse>>>;

   

    internal sealed class Handler : IRequestHandler<Query, Result<PagedResult<GetSafetyEventsResponse>>>
    {
        private readonly IGenericRepository<SafetyEvent, Guid> _SafetyEventRepo;
        private readonly IFileStorageService _fileService;

        public Handler(IGenericRepository<SafetyEvent, Guid> SafetyEventRepo, IFileStorageService fileService)
        {
            _SafetyEventRepo = SafetyEventRepo;
            _fileService = fileService;
        }

        public async Task<Result<PagedResult<GetSafetyEventsResponse>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var query = _SafetyEventRepo.GetAll(trackChanges: false);

            if (request.DriverId.HasValue)
                query = query.Where(e => e.DriverId == request.DriverId);

            if (request.CompanyId.HasValue)
            {
                query = query.Where(e => e.Driver != null && e.Driver.CompanyId == request.CompanyId);
            }

            if (request.FromDate.HasValue)
                query = query.Where(e => e.Timestamp >= request.FromDate.Value);

            if (request.ToDate.HasValue)
                query = query.Where(e => e.Timestamp <= request.ToDate.Value);

            if (request.MinLevel.HasValue)
                query = query.Where(e => e.AlertLevel <= request.MinLevel.Value);


            var pagedRawData = await query
                .OrderByDescending(e => e.Timestamp)
                .Select(e => new
                {
                    e.Id,
                    e.Message,
                    e.DriverState,
                    e.AlertLevel,
                    e.S3DriverImagePath, 
                    e.S3RoadImagePath,   
                    e.Timestamp,
                    VehiclePlate = e.Vehicle != null ? e.Vehicle.PlateNumber : "N/A",
                    DriverName = e.Driver != null ? e.Driver.FullName : "N/A"
                })
                .ToPagedResultAsync(request.Page, request.PageSize, cancellationToken); 

            var mappedItems = pagedRawData.Items.Select(e => new GetSafetyEventsResponse(
                e.Id,
                e.Message ?? "N/A",
                e.DriverState,
                e.AlertLevel,
                _fileService.GetPresignedUrl(e.S3DriverImagePath), 
                _fileService.GetPresignedUrl(e.S3RoadImagePath),   
                e.Timestamp,
                e.VehiclePlate ?? "N/A",
                e.DriverName
            ));

            var finalResult = new PagedResult<GetSafetyEventsResponse>(
                mappedItems,
                pagedRawData.Pagination.TotalItems,
                pagedRawData.Pagination.Page,
                pagedRawData.Pagination.PageSize
            );

            return Result<PagedResult<GetSafetyEventsResponse>>.Success(finalResult);
        }
    }
}