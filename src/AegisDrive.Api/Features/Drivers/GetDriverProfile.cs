using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.Drivers;
using AegisDrive.Api.Entities;
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace AegisDrive.Api.Features.Drivers;

public static class GetDriverProfile
{
    public record Query(int DriverId) : IRequest<Result<GetDriverProfileResponse>>;

    internal sealed class Handler : IRequestHandler<Query, Result<GetDriverProfileResponse>>
    {
        private readonly IGenericRepository<Driver, int> _driversRepository;
        private readonly IFileStorageService _fileStorageService;
        private readonly IDatabase _redis; 

        public Handler(IGenericRepository<Driver, int> driversRepository,IFileStorageService fileStorageService,IConnectionMultiplexer redisMux) 
        {
            _driversRepository = driversRepository;
            _fileStorageService = fileStorageService;
            _redis = redisMux.GetDatabase();
        }

        public async Task<Result<GetDriverProfileResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            string cacheKey = $"driver:{request.DriverId}:profile";
            GetDriverProfileResponse? driverDto = null;

            // 1. Try to get from Redis Cache first
            var cachedData = await _redis.StringGetAsync(cacheKey);
            if (!cachedData.IsNull)
            {
                driverDto = JsonSerializer.Deserialize<GetDriverProfileResponse>(cachedData.ToString());
            }

            // 2. If not in Cache, query Database
            if (driverDto is null)
            {
                driverDto = await _driversRepository.GetAll()
                    .Where(d => d.Id == request.DriverId)
                    .Select(d => new GetDriverProfileResponse(
                        d.FullName,
                        d.PhoneNumber,
                        d.Email,
                        d.IsActive,
                        d.SafetyScore,
                        d.Company == null ? null : new CompanyDto(
                            d.Company.Name,
                            d.Company.RepresentativeName,
                            d.Company.RepresentativeEmail,
                            d.Company.RepresentativePhone
                        ),
                        d.FamilyMembers.Select(fm => new FamilyMemberDto(
                            fm.FullName,
                            fm.PhoneNumber,
                            fm.Email,
                            fm.Relationship,
                            fm.NotifyOnCritical
                        )).ToList()
                    )
                    {
                        // Store the RAW S3 Key here (e.g. "fleets/1/profile.jpg")
                        PictureUrl = d.PictureUrl
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (driverDto is null)
                {
                    return Result<GetDriverProfileResponse>.Failure<GetDriverProfileResponse>(new Error("Driver.NotFound", $"Driver with ID {request.DriverId} was not found."));
                }

                // 3. Save to Redis (Cache the RAW key version)
                // Expiry: 30 minutes (Adjust based on how often driver profiles change)
                var serializedData = JsonSerializer.Serialize(driverDto);
                await _redis.StringSetAsync(cacheKey, serializedData, TimeSpan.FromMinutes(30));
            }

            // 4. Generate Presigned URL (Always runs, ensuring URL is fresh/valid)
            // We do this AFTER caching so we don't cache a URL that might expire in 15 mins
            if (!string.IsNullOrEmpty(driverDto.PictureUrl))
            {
                driverDto.PictureUrl = _fileStorageService.GetPresignedUrl(driverDto.PictureUrl);
            }

            return Result<GetDriverProfileResponse>.Success(driverDto);
        }
    }
}