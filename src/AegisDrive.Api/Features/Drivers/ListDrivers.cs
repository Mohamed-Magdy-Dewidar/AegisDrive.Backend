using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Drivers;
using AegisDrive.Api.DataBase;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Shared;
using AegisDrive.Api.Shared.Pagination;
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AegisDrive.Api.Features.Drivers;

public static class ListDrivers
{
    public record Query(int? CompanyId, bool? IsActive, int Page = 1, int PageSize = 20) : IRequest<Result<PagedResult<ListDriversResponse>>>;

    internal sealed class Handler : IRequestHandler<Query, Result<PagedResult<ListDriversResponse>>>
    {
        private readonly IGenericRepository<Driver ,int> _driverRepository;
        private readonly IFileStorageService _fileStorageService;

        public Handler(IGenericRepository<Driver, int> driverRepository, IFileStorageService fileStorageService)
        {
            _driverRepository = driverRepository;
            _fileStorageService = fileStorageService;
        }

        public async Task<Result<PagedResult<ListDriversResponse>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var query = _driverRepository.GetAll();

            if (request.CompanyId.HasValue)
                query = query.Where(d => d.CompanyId == request.CompanyId.Value);
            else
                query = query.Where(d => d.CompanyId == null);


            if (request.IsActive.HasValue)
                query = query.Where(d => d.IsActive == request.IsActive.Value);

            var dtoQuery = query
                .OrderByDescending(d => d.CreatedOnUtc)
                .Select(d => new ListDriversResponse(
                    d.FullName,
                    d.PhoneNumber,
                    d.Email,
                    d.IsActive,
                    d.SafetyScore,
                    d.Company != null ? d.Company.Name : null
                )
                {
                    // We assume the PictureUrl column holds the S3 Key here
                    PictureUrl = d.PictureUrl
                });

            var pagedResult = await dtoQuery.ToPagedResultAsync(request.Page, request.PageSize, cancellationToken);

            var s3Keys = pagedResult.Items
                .Where(d => !string.IsNullOrEmpty(d.PictureUrl))
                .Select(d => d.PictureUrl!)
                .Distinct()
                .ToList();

            if (s3Keys.Count > 0)
            {
                var urls = _fileStorageService.GetPresignedUrls(s3Keys);

                foreach (var driver in pagedResult.Items)
                {
                    if (!string.IsNullOrEmpty(driver.PictureUrl) &&
                        urls.TryGetValue(driver.PictureUrl, out var signedUrl))
                    {
                        driver.PictureUrl = signedUrl;
                    }
                }
            }

            return Result<IEnumerable<GetDriverFamilyMembersResponse>>.Success(pagedResult);
        }
    }
}